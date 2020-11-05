﻿using System.Collections.Generic;

namespace MigrationTools._EngineV1.Configuration.Processing
{
    public class ExportProfilePictureFromADConfig : IProcessorConfig
    {
        public string Domain { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string PictureEmpIDFormat { get; set; }

        /// <inheritdoc />
        public bool Enabled { get; set; }

        /// <inheritdoc />
        public string Processor
        {
            get { return "ExportProfilePictureFromADContext"; }
        }

        /// <inheritdoc />
        public bool IsProcessorCompatible(IReadOnlyList<IProcessorConfig> otherProcessors)
        {
            return true;
        }
    }
}