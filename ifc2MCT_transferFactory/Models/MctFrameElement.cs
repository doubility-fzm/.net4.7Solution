using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public class MctFrameElement:MctElement
    {
        public double Angle { get; set; }
        public double SubType { get; set; }
        public override string ToString()
        {
            if (Type != MctElementTypeEnum.BEAM && Type != MctElementTypeEnum.TRUSS)
                throw new ArgumentException("Incorrect element type");
            return string.Format($"{base.ToString()},{Angle},{SubType}");
        }
        public MctFrameElement((double angle,double subtype) extra_info)
        {
            Angle = extra_info.angle;
            SubType = extra_info.subtype;
        }
        public MctFrameElement()
        {
            Angle = 0;
            SubType = 0;
        }
    }
}
