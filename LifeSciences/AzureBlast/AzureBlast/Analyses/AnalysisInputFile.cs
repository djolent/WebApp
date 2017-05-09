// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Azure.Batch.Blast.Analyses
{
    public class AnalysisInputFile
    {
        public string Filename { get; set; }

        public long Length { get; set; }

        public Stream Content { get; set; }
    }
}
