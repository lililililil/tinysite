﻿using System;

namespace TinySite.Models
{
    public class SiteConfig
    {
        public SiteConfig()
        {
            this.Metadata = new MetadataCollection();
        }

        public Author Author { get; set; }

        public string DocumentsPath { get; set; }

        public string FilesPath { get; set; }

        public string LayoutsPath { get; set; }

        public string OutputPath { get; set; }

        public SiteConfig[] SubsiteConfigs { get; set; }

        public string Url { get; set; }

        public string RootUrl { get; set; }

        public SiteConfig Parent { get; set; }

        public TimeZoneInfo TimeZone { get; set; }

        public MetadataCollection Metadata { get; set; }
    }
}
