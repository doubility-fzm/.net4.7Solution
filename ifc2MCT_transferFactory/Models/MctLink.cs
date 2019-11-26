using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public class MctLink
    {
        public int linkIndex { get; set; }
        public enum MctLinkTypeEnum
        {
            RIGD=0,ELNK
        }
        public MctLinkTypeEnum type { get; set; }
        public int linkKey { get; set; }

        public MctLink()
        {
            //empty
        }
        public override string ToString()
        {
            return $"{linkIndex},{type},{linkKey}";
        }
    }
}
