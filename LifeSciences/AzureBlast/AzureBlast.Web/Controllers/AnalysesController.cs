// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Microsoft.Azure.Batch.Blast.Configuration;
using Microsoft.Azure.Batch.Blast.Analyses;
using Microsoft.Azure.Blast.Web.Models;

namespace Microsoft.Azure.Blast.Web.Controllers
{
    public class AnalysesController : AuthorizedController
    {
        private readonly BlastConfiguration _configuration;
        private readonly IAnalysisProvider _analysisProvider;

        public AnalysesController(BlastConfiguration configuration, IAnalysisProvider analysisProvider)
        {
            _configuration = configuration;
            _analysisProvider = analysisProvider;
        }

        [Route("analyses")]
        public ActionResult Index()
        {
            return View();
        }

        [Route("analyses/new")]
        public ActionResult New()
        {
            var model = new NewAnalysisModel
            {
                BlastExecutables = new List<string>(new string[] {"blastp", "blastn", "blastx", "tblastn", "tblastx"}),
                VirtualMachineSizes = _configuration.GetVirtualMachineSizes(),
            };
            return View(model);
        }

        [Route("analyses/show/{analysisId}")]
        public ActionResult Show(string analysisId)
        {
            Guid id;
            if (!Guid.TryParse(analysisId, out id))
            {
                return HttpNotFound();
            }

            var analysis = _analysisProvider.GetAnalysis(id);
            if (analysis == null)
            {
                return HttpNotFound();
            }

            return View(analysis);
        }

        [Route("analyses/show/{analysisId}/{queryId}/visualize/{resultXmlFile}")]
        public ActionResult Visualize(string analysisId, string queryId, string resultXmlFile)
        {
            Guid parsedAnalysisId;
            if (!Guid.TryParse(analysisId, out parsedAnalysisId))
            {
                return HttpNotFound(string.Format("Unable to parse analysisId: {0}", analysisId));
            }

            int parsedQueryId;
            if (!Int32.TryParse(queryId, out parsedQueryId))
            {
                return HttpNotFound(string.Format("Unable to parse queryId: {0}", queryId));
            }

            var analysis = _analysisProvider.GetAnalysis(parsedAnalysisId);
            if (analysis == null)
            {
                return HttpNotFound();
            }

            return View(new VisualizeResultsModel
            {
                Id = analysisId,
                QueryId = parsedQueryId,
                AnalysisName = analysis.Name,
                Filename = resultXmlFile
            });
        }
    }
}
