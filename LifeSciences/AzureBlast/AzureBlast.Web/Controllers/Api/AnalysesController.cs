// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Microsoft.Azure.Batch.Blast.Analyses;
using Microsoft.Azure.Batch.Blast.Storage.Entities;

namespace Microsoft.Azure.Blast.Web.Controllers.Api
{
    [RoutePrefix("api")]
    public class AnalysesController : BaseApiController
    {
        private readonly IAnalysisProvider _analysisProvider;

        public AnalysesController(IAnalysisProvider analysisProvider)
        {
            _analysisProvider = analysisProvider;
        }

        [Route("analyses"), HttpGet]
        public IEnumerable<AnalysisEntity> GetAll()
        {
            return _analysisProvider.ListAnalyses().OrderByDescending(s => s.StartTime);
        }

        [Route("analyses/{analysisId}/queries/{queryId}/outputs/{filename}"), HttpGet]
        public HttpResponseMessage GetAnalysisQueries(Guid analysisId, string queryId, string filename)
        {
            var response = _analysisProvider.GetAnalysisQueryOutput(analysisId, queryId, filename);
            return Request.CreateResponse(HttpStatusCode.OK, response);
        }

        [Route("analyses/{analysisId}/queries"), HttpGet]
        public IEnumerable<AnalysisQueryEntity> GetAnalysisQueries(Guid analysisId)
        {
            return _analysisProvider.ListAnalysisQueries(analysisId).OrderBy(q => q.QueryFilename);
        }

        [Route("analyses/{analysisId}"), HttpGet]
        public HttpResponseMessage Get(Guid analysisId)
        {
            var analysis = _analysisProvider.GetAnalysis(analysisId);
            if (analysis == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, string.Format("analysisId: {0} was not found", analysisId));
            }

            return Request.CreateResponse(HttpStatusCode.OK, analysis);
        }

        [Route("analyses/{analysisId}"), HttpDelete]
        public HttpResponseMessage Delete(Guid analysisId)
        {
            var analysis = _analysisProvider.GetAnalysis(analysisId);
            if (analysis == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, string.Format("analysisId: {0} was not found", analysisId));
            }

            _analysisProvider.DeleteAnalysis(analysisId);

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Route("analyses/{analysisId}/actions/cancel"), HttpPost]
        public HttpResponseMessage Cancel(Guid analysisId)
        {
            var analysis = _analysisProvider.GetAnalysis(analysisId);

            if (analysis == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, string.Format("analysisId: {0} was not found", analysisId));
            }

            _analysisProvider.CancelAnalysis(analysisId);

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Route("analyses"), HttpPost]
        public async Task<HttpResponseMessage> SubmitAnalysis()
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            HttpContext.Current.Server.ScriptTimeout = 900;

            var root = HttpContext.Current.Server.MapPath("~/App_Data");
            var provider = new MultipartFormDataStreamProvider(root);

            try
            {
                // Read the async multipart form data.
                await Request.Content.ReadAsMultipartAsync(provider);

                // read the form data into a analysis model
                var analysisModel = CreateAnalysisModel(provider.FormData);

                var analysisInputFiles = new List<AnalysisInputFile>(analysisModel.AnalysisInputFiles);

                // get the contents of the files from the request
                foreach (MultipartFileData file in provider.FileData)
                {
                    var fileInfo = new FileInfo(file.LocalFileName);

                    using (var reader = fileInfo.OpenText())
                    {
                        var fullFilename = file.Headers.ContentDisposition.FileName.Replace("\"", "");

                        if (analysisModel.SplitSequenceFile)
                        {
                            var filename = Path.GetFileNameWithoutExtension(fullFilename);
                            var extension = Path.GetExtension(fullFilename);

                            int currentSequenceCount = 0;
                            int currentFileCount = 1;
                            string currentSequenceContent = null;
                            string sequenceFilename;

                            var line = reader.ReadLine();
                            while (line != null)
                            {
                                if (string.IsNullOrEmpty(line) || line.Trim() == "")
                                {
                                    continue;
                                }

                                if (line.StartsWith(">"))
                                {
                                    if (string.IsNullOrEmpty(currentSequenceContent))
                                    {
                                        // We're the first sequence
                                        currentSequenceContent = line;
                                    }
                                    else
                                    {
                                        currentSequenceCount++;

                                        if (currentSequenceCount % analysisModel.SequencesPerQuery == 0)
                                        {
                                            // Flush previous sequence(s)
                                            sequenceFilename = string.Format("{0}_part{1}{2}",
                                                filename, currentFileCount++, extension);
                                            analysisInputFiles.Add(new AnalysisInputFile
                                            {
                                                Filename = sequenceFilename,
                                                Length = Encoding.UTF8.GetByteCount(currentSequenceContent),
                                                Content = new MemoryStream(Encoding.UTF8.GetBytes(currentSequenceContent)),
                                            });

                                            currentSequenceContent = line;
                                        }
                                        else
                                        {
                                            // Keep appending
                                            currentSequenceContent += "\n" + line;
                                        }
                                    }
                                }
                                else
                                {
                                    currentSequenceContent += "\n" + line;
                                }

                                line = reader.ReadLine();
                            }

                            if (!string.IsNullOrEmpty(currentSequenceContent))
                            {
                                // Flush the final one
                                sequenceFilename = string.Format("{0}_part{1}{2}",
                                    filename, currentFileCount, extension);
                                analysisInputFiles.Add(new AnalysisInputFile
                                {
                                    Filename = sequenceFilename,
                                    Length = Encoding.UTF8.GetByteCount(currentSequenceContent),
                                    Content = new MemoryStream(Encoding.UTF8.GetBytes(currentSequenceContent)),
                                });
                            }
                        }
                        else
                        {
                            var sequenceText = reader.ReadToEnd();
                            analysisInputFiles.Add(new AnalysisInputFile
                            {
                                Filename = fullFilename,
                                Length = fileInfo.Length,
                                Content = new MemoryStream(Encoding.UTF8.GetBytes(sequenceText)),
                            });
                        }
                    }

                    try
                    {
                        fileInfo.Delete();
                    }
                    catch
                    {
                        // don't really care too much if this fails
                        Trace.WriteLine(string.Format("failed to delete temp file: {0}", file.LocalFileName));
                    }
                }

                analysisModel.AnalysisInputFiles = analysisInputFiles;

                // do some basic server side validation ...
                if (String.IsNullOrEmpty(analysisModel.Name))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        "The name must be provided with the analysis");
                }

                if (String.IsNullOrEmpty(analysisModel.DatabaseName))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        "The database name must be provided with the analysis");
                }

                if (String.IsNullOrEmpty(analysisModel.Executable))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        "The program name must be provided with the analysis");
                }

                if (analysisModel.SplitSequenceFile && analysisModel.SequencesPerQuery < 1)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        "Sequences per query must ne greater than 0 when splitting a sequence file.");
                }

                if (!analysisModel.AnalysisInputFiles.Any())
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        "You must enter either a plain text sequence, or upload file/files contining the sequence(s) to analysis");
                }

                var id = _analysisProvider.SubmitAnalysis(analysisModel);

                return Request.CreateResponse(HttpStatusCode.OK, id);
            }
            catch (Exception e)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e.ToString());
            }
        }

        private AnalysisSpecification CreateAnalysisModel(NameValueCollection formData)
        {
            // note: only to see what is in here, can be removed
            foreach (var key in formData.AllKeys)
            {
                foreach (var val in formData.GetValues(key))
                {
                    Trace.WriteLine(string.Format("{0}: {1}", key, val));
                }
            }

            var spec = new AnalysisSpecification
            {
                Name = formData["analysisName"],
                DatabaseName = formData["databaseName"],
                Executable = formData["executable"],
                ExecutableArgs = formData["executableArgs"],
                AnalysisInputFiles = new List<AnalysisInputFile>(),
                SplitSequenceFile = ToBoolean(formData["splitSequenceFile"]),
                SequencesPerQuery = ToInt(formData["seqencesPerQuery"], 1),
            };

            AddPoolSpecToAnalysis(formData, spec);

            if (formData["analysisSequenceText"] != null && !string.IsNullOrEmpty(formData["analysisSequenceText"].Trim()))
            {
                var bytes = Encoding.UTF8.GetBytes(formData["analysisSequenceText"]);

                spec.AnalysisInputFiles = new List<AnalysisInputFile>()
                {
                    new AnalysisInputFile
                    {
                        Filename = "analysissequence.txt",
                        Content = new MemoryStream(bytes),
                        Length = bytes.Length,
                    }
                };
            }

            return spec;
        }

        private void AddPoolSpecToAnalysis(NameValueCollection formData, AnalysisSpecification spec)
        {
            if (string.IsNullOrEmpty(formData["poolId"]) && (string.IsNullOrEmpty(formData["targetDedicated"]) || string.IsNullOrEmpty(formData["virtualMachineSize"])))
            {
                throw new Exception("An existing PoolId or targetDedicated and virtualMachineSize must be specified");
            }

            if (!string.IsNullOrEmpty(formData["poolId"]))
            {
                spec.PoolId = formData["poolId"];
            }
            else
            {
                spec.TargetDedicated = int.Parse(formData["targetDedicated"]);
                spec.VirtualMachineSize = formData["virtualMachineSize"];
                spec.PoolDisplayName = formData["poolName"];
            }
        }

        private bool ToBoolean(string formValue)
        {
            bool value;
            if (Boolean.TryParse(formValue, out value))
            {
                return value;
            }
            return false;
        }

        private int ToInt(string formValue, int defaultValue)
        {
            int value;
            if (int.TryParse(formValue, out value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}
