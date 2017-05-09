// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Batch.Blast.Analyses
{
    public enum AnalysisState
    {
        StagingData,
        WaitingForResources,
        DownloadingDatabase,
        Running,
        Complete,
        Canceled,
        Error
    }
}
