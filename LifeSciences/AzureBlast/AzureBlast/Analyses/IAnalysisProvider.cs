// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Batch.Blast.Storage.Entities;

namespace Microsoft.Azure.Batch.Blast.Analyses
{
    public interface IAnalysisProvider
    {
        Guid SubmitAnalysis(AnalysisSpecification analysisSpec);

        AnalysisEntity GetAnalysis(Guid analysisId);

        void DeleteAnalysis(Guid analysisId);

        void CancelAnalysis(Guid analysisId);

        IEnumerable<AnalysisEntity> ListAnalyses();

        IEnumerable<AnalysisQueryEntity> ListAnalysisQueries(Guid analysisId);

        string GetAnalysisQueryOutput(Guid analysisId, string queryId, string filename);
    }
}
