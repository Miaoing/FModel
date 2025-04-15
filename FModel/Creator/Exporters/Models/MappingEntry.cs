using System;
using System.Collections.Generic;

namespace FModel.Creator.Exporters.Models
{
    public class MappingEntry
    {
        public string MeshPath { get; set; }
        public string MaterialPath { get; set; }
        public string DiffuseTexturePath { get; set; }
        public string NormalTexturePath { get; set; }
        public string SpecularTexturePath { get; set; }
        public string EmissiveTexturePath { get; set; }
        public HashSet<string> AllTexturePaths { get; set; } = new HashSet<string>();
    }
} 