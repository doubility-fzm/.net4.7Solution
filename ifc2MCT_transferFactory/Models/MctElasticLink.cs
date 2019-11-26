using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public class MctElasticLink
    {
        public int linkNum { get; set; }
        public MctNode node1 { get; set; }
        public MctNode node2 { get; set; }
        public enum MctLinkTypeEnums
        {
            GEN=0,RIGHD,TENSORCOMP,MUTILINEAR
        }
        public MctLinkTypeEnums Type { get; set; }
        public double angle { get; set; }
        public double SDx { get; set; }
        public double SDy { get; set; }
        public double SDz { get; set; }
        public double SRx { get; set; }
        public double SRy { get; set; }
        public double SRz { get; set; }
        public bool IsShear { get; set; }
        public double DRy { get; set; }
        public double DRz { get; set; }
        public string Group { get; set; }
        public MctElasticLink()
        {
            Type = MctLinkTypeEnums.GEN;
            angle = 0;
            SDx = 2.06e8;
            SDy = 0;
            SDz = 0;
            SRx = 0;
            SRy = 0;
            SRz = 0;
            IsShear = false;
            DRy = 0.5;
            DRz = 0.5;
            Group = "";
        }
    }
}
