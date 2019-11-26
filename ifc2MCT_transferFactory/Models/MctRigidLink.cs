using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public class MctRigidLink
    {
        public int _linkKey { get; set; }
        public MctNode _mainNode { get; set; }
        public List<bool> DOF { get; set; }
        public List<MctNode> _subNodes { get; set; }
        public string group { get; set; }

        public MctRigidLink()
        {
            DOF = new List<bool>() { true, true, true, true, true, true };
            group = "";
        }

        public MctRigidLink(int key,MctNode mainNode,List<MctNode> subNodes)
        {
            _linkKey = key;
            _mainNode = mainNode;
            DOF = new List<bool>() { true, true, true, true, true, true };
            _subNodes = subNodes;
            group = "";
        }

        public override string ToString()
        {
            string rigitLink = "";
            rigitLink += $"{_linkKey},{_mainNode.Id.ToString()},";
            foreach(var constrain in DOF)
            {
                rigitLink += $"{(constrain ? 1 : 0)}";
            }
            rigitLink += ",";
            foreach(var node in _subNodes)
            {
                rigitLink += $"{node.Id.ToString()} ";
            }

            return rigitLink;
        }
    }
}
