using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public abstract class MctSupport
    {
        public readonly Dictionary<List<bool>, List<MctNode>> _bearingTypePair = new Dictionary<List<bool>, List<MctNode>>();
    }
}
