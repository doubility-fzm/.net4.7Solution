using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ifc2MCT_transferFactory.Models;

namespace ifc2MCT_transferFactory
{
    public class MctStore
    {
        private readonly Dictionary<long, MctNode> _nodes = new Dictionary<long, MctNode>();
        private readonly Dictionary<int, MctMaterialValue> _material = new Dictionary<int, MctMaterialValue>();
        private readonly Dictionary<long, MctElement> _elements = new Dictionary<long, MctElement>();
        private readonly Dictionary<int, MctSection> _sections = new Dictionary<int, MctSection>();
        private readonly HashSet<MctCommonSupport> _supports = new HashSet<MctCommonSupport>();
        //private readonly Dictionary<int, (List<bool>, List<MctNode>)> _supports = new Dictionary<int, (List<bool>, List<MctNode>)>();
        public MctUnitSystem UnitSystem {get;set;}

        public void AddNode(MctNode node)
        {
            if(!_nodes.ContainsKey(node.Id))
            {
                _nodes[node.Id] = node;
            }
        }
        public void AddNode(List<MctNode> nodelist)
        {
            foreach (var node in nodelist)
                AddNode(node);
        }

        public void AddMaterial(List<MctMaterialValue> materialSet)
        {
            var filteredMaterialSet = new Dictionary<string, MctMaterialValue>();
            int count = 1;
            foreach (var single in materialSet)
            {
                filteredMaterialSet[single.Name] = single;
            }
            foreach (var mat in filteredMaterialSet)
            {
                mat.Value.Id = count++;
            }
            foreach (var material in filteredMaterialSet)
            {
                AddMaterial(material.Value);
            }
        }
        public void AddMaterial(MctMaterialValue material)
        {
            if(!_material.ContainsKey(material.Id))
            {
                _material[material.Id] = material;
            }
        }
        public void AddSection(MctSection sec,ref int secId)
        {
            if (_sections.Count==0)
            {
                _sections[secId-1] = sec;
                secId++;
            }
            else
            {
                foreach (var section in _sections)
                {
                    if (sec.Name != section.Value.Name)
                    {
                        _sections[secId] = sec;
                        secId++;
                        break;
                    }
                }
            }
        }

        public void AddElement(MctElement element)
        {
            if (!_elements.ContainsKey(element.Id))
                _elements[element.Id] = element;
        }

        public void AddSupport(MctNode node,List<bool> constraints)
        {
            foreach(var support in _supports)
            {
                if(support is MctCommonSupport cs && cs.IsSameBearingType(constraints))
                {
                    cs.AddNode(node);
                    return;
                }
            }
            AddSupport(new MctCommonSupport(new List<MctNode>() { node},constraints));
        }
        public void AddSupport(MctCommonSupport support)
        {
            _supports.Add(support);
        }

        public Dictionary<long, MctNode> GetGirderNodeSet()
        {
            return _nodes;
        }

        public void WriteMctFile(string path)
        {
            var sw = new StreamWriter(path, false, Encoding.GetEncoding("GB2312"));
            string head = ";--------------------------------------\n" +
                    ";\tMIDAS/Civil Text(MCT) File\n" +
                    $";\tDate/Time : {DateTime.Now}\n" +
                    ";\tProduced by : ifc2mct.MctFactory\n" +
                    ";\tAuthor : Fu Zhongmin\n" +
                    ";--------------------------------------";
            string version = "*VERSION\n" + "\t8.8.0\n";
            sw.WriteLine(head);
            sw.WriteLine(version);
            sw.WriteLine(UnitSystem);

            string username = "doubility";
            string address = "DESKTOP-2IO6Q8N";
            string project_info = "*PROJINFO\t;Project Information\n" +
                "; PROJECT, REVISION, USER, EMAIL, ADDRESS, TEL, FAX, CLIENT, TITLE, ENGINEER, EDATE       ; One Line per Data\n" +
                "; CHECK1, CDATE1, CHECK2, CDATE2, CHECK3, CDATE3, APPROVE, ADATE, COMMENT                 ; One Line per Data\n";
            sw.WriteLine(project_info);
            sw.WriteLine("\tUSER={0}", username);
            sw.WriteLine("\tADDRESS={0}", address);

            head = "\n*MATERIAL\t; Materials";
            sw.WriteLine(head);
            foreach (var mat in _material)
            {
                sw.WriteLine(mat.Value);
            }

            head = "\n*NODE\t; Nodes\n; iNO, X, Y, Z";
            sw.WriteLine(head);
            foreach (var node in _nodes)
            {
                sw.WriteLine(node.Value);
            }

            head = "\n*ELEMENT    ; Elements";
            sw.WriteLine(head);
            foreach(var element in _elements)
            {
                sw.WriteLine(element.Value);
            }

            head = "\n*SECTION\t; Section";
            sw.WriteLine(head);
            foreach(var sec in _sections)
            {
                sw.WriteLine(sec.Value);
            }

            head = "\n*CONSTRAINT    ; Supports";
            sw.WriteLine(head);
            foreach(var support in _supports)
            {
                sw.WriteLine(support);
            }

            sw.Close();
        }
    }
}
