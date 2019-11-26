using System;
using System.Collections.Generic;
using System.Text;

namespace ifc2MCT_transferFactory.Models
{
    public class MctCommonSupport:MctSupport
    {
        private List<MctNode> _nodes = new List<MctNode>();
        readonly List<bool> _constraints = new List<bool>();
        public MctCommonSupport(List<MctNode> nodes, bool dx, bool dy, bool dz, bool rx, bool ry, bool rz)
        {
            var tmpConstraint = new List<bool>() { dx, dy, dz, rx, ry, rz };
            AddNode(nodes, tmpConstraint);
        }
        public void AddNode(MctNode node)
        {
            if (!_nodes.Exists(n => n.Id == node.Id))
                _nodes.Add(node);
        }
        public void AddNode(List<MctNode> nodes)
        {
            foreach (var node in nodes)
                AddNode(node);
        }

        public MctCommonSupport(List<MctNode> nodes, List<bool> constraints)
        {
            AddNode(nodes);
            if (constraints.Count != 6)
                throw new ArgumentException("Constraints must have 6 boolean values");
            for (int i = 0; i < 6; ++i)
                _constraints.Add(constraints[i]);
        }

        public void AddNode(MctNode node,List<bool> constraints)
        {
            if(_bearingTypePair.Keys.Count==0)
            {
                _bearingTypePair.Add(constraints, _nodes);
                _bearingTypePair[constraints].Add(node);
            }
            if(!IsSameBearingType(constraints))
            {
                _bearingTypePair.Add(constraints, _nodes);                        
            }
            _bearingTypePair[constraints].Add(node);
        }
        public void AddNode(List<MctNode> nodes,List<bool> constraints)
        {
            foreach(var node in nodes)
            {
                AddNode(node,constraints);
            }
        }
        public bool IsSameBearingType(List<bool> constraint)
        {
            const int CONSTRNUM = 6;
            int count = constraint.Count;
            if (count != CONSTRNUM)
                return false;
            for (int i = 0; i < CONSTRNUM; ++i)
                if (_constraints[i] != constraint[i])
                    return false;
            return true;
        }

        public override string ToString()
        {
            string nodeList = "";
            foreach (var node in _nodes)
                nodeList += $"{node.Id.ToString()} ";
            string constraints = "";
            foreach (var constraint in _constraints)
                constraints += $"{(constraint ? 1 : 0)}";
            string group = "";
            return $"{nodeList},{constraints},{group}";
        }
    }
}
