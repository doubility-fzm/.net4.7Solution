using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ifc2MCT_transferFactory;
using ifc2MCT_transferFactory.Models;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;
using Utilities;

namespace ifc2MCT
{
    public class TranslateAdaptor
    {
        //temporary store the ifcModel
        private readonly IfcStore _ifcModel;
        private readonly Dictionary<int, SortedList<double, bool>> _pointPlacementTable =
            new Dictionary<int, SortedList<double, bool>>();
        private readonly MctStore _mctStore = new MctStore();

        private List<MctNode> girderNodeSet = new List<MctNode>();

        //转换横梁节点用的变量
        private List<MctNode> LateralBeamNodeSet = new List<MctNode>();
        private int _lateralBeamNums { get; set; }
        private List<IIfcProxy> Bearings { get; set; }
        private int matIndex = 1;
        private int nodeIndex = 0;
        private int sectionIndex = 1;
        private long elementIndex = 1;
        private int elasticLinkIndex = 1;
        private int rigidLinkIndex = 1;
        public TranslateAdaptor(string path)
        {
            if (File.Exists(path))
            {
                _ifcModel = IfcStore.Open(path);
            }
            else
            {
                throw new ArgumentException("Unable to open ifcModel,check out whether the path is existed");
            }
            Initialise();
        }
        public TranslateAdaptor(IfcStore model)
        {
            _ifcModel = model;
            Initialise();
        }
        private void Initialise()
        {
            try
            {
                if (_ifcModel == null)
                {
                    throw new InvalidOperationException("Empty model cannot be processed");
                }
                InitialiseUnitSystem();
                if (!_ifcModel.Instances.OfType<IIfcAlignment>().Any())
                {
                    throw new InvalidOperationException("Model without IfcAlignment cannot be processed");
                }
                if (_ifcModel.Instances.OfType<IIfcAlignment>().FirstOrDefault().Axis == null)
                {
                    throw new InvalidOperationException("Model without IfcAlignmentCurve cannot be processed");
                }
                var diretrices = _ifcModel.Instances.OfType<IIfcOffsetCurveByDistances>().
                    Where(ocbd => ocbd.BasisCurve is IIfcAlignmentCurve);
                foreach (var diretrix in diretrices)
                {
                    _pointPlacementTable[diretrix.EntityLabel] = new SortedList<double, bool>();
                }
                //没有添加支座的初始化信息
                Bearings = _ifcModel.Instances.OfType<IIfcProxy>().Where(b => b.ObjectType == "POT").ToList();
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        private void InitialiseUnitSystem()
        {
            var siUnits = _ifcModel.Instances.OfType<IIfcProject>().FirstOrDefault().UnitsInContext.Units.OfType<IIfcSIUnit>();
            var ifcForceUnit = siUnits.Where(u => u.UnitType == IfcUnitEnum.FORCEUNIT).FirstOrDefault();
            var forceUnit = ifcForceUnit == null ? MctForceUnitEnum.N :
                (ifcForceUnit.Prefix == IfcSIPrefix.KILO ? MctForceUnitEnum.kN : MctForceUnitEnum.N);
            var ifcLengthUnit = siUnits.Where(u => u.UnitType == IfcUnitEnum.LENGTHUNIT).FirstOrDefault();
            var lengthUnit = ifcLengthUnit == null ? MctLengthUnitEnum.MM :
                (ifcLengthUnit.Prefix == IfcSIPrefix.CENTI ? MctLengthUnitEnum.CM :
                (ifcLengthUnit.Prefix == null) ? MctLengthUnitEnum.M : MctLengthUnitEnum.MM);
            var temperUnit = MctTemperUnitEnum.C;
            var heatUnit = MctHeatUnitEnum.J;
            _mctStore.UnitSystem = new MctUnitSystem()
            {
                ForceUnit = forceUnit,
                LengthUnit = lengthUnit,
                TemperUnit = temperUnit,
                HeatUnit = heatUnit
            };
        }

        public void TranslateGirder()
        {
            var girderSet = _ifcModel.Instances.OfType<IIfcElementAssembly>().
                Where(ea => ea.PredefinedType == IfcElementAssemblyTypeEnum.GIRDER).ToList();
            if(girderSet.Count==0)
            {
                throw new InvalidOperationException("A bridge without superstructure cannot be processed");
            }

            List<IIfcMaterialSelect> girderSet_Material = new List<IIfcMaterialSelect>();
            foreach(var girder in girderSet)
            {
                girderSet_Material.Add(girder.Material);
            }

            //parse BridgeDeckMat
            var deckSlab = _ifcModel.Instances.OfType<IIfcSlab>().FirstOrDefault();
            if(deckSlab==null)
            {
                throw new InvalidOperationException("Parse BrigdeDeck Failed");
            }
            girderSet_Material.Add(deckSlab.Material);

            var mat = TranslatorToolkitFactory.TranslateMaterial(girderSet_Material,ref matIndex);
            _mctStore.AddMaterial(mat);

            //parse diretrix
            GetLateralGirderNums(girderSet.Count);

            //parse section
            var dimensions = new List<List<double>>();
            foreach (var girder in girderSet)
            {
                PreProcessDirectrix(girder);
                dimensions.Add(TranslatorToolkitFactory.ParseSectionDimensions(girder));
            }
            //20191111
            var deckInfo = TranslatorToolkitFactory.ParseDeckDimensions(deckSlab);

            //循环完毕之后表示收集到所有的支座信息
            var bearingMap = new Dictionary<MctNode, IIfcProxy>();
            foreach (var position in _pointPlacementTable)
            {
                var coordinates = position.Value.Select(p => p.Key).ToList();
                var nodes = Utilities.TranslatorToolkitFactory.TranslateNodes((IIfcCurve)_ifcModel.Instances[position.Key], coordinates,nodeIndex++);
                _mctStore.AddNode(nodes);
                foreach(var node in nodes)
                {
                    girderNodeSet.Add(node);
                }
                int nodeNums = position.Value.Keys.Count;
                MctSection sec= null;
                for (int i=0;i<nodeNums-1;i++)
                {
                    double currentPos = position.Value.Keys[i];
                    double nextPos = position.Value.Keys[i + 1];
                    bool isSectionChanged = position.Value.Values[i];
                    sec = TranslatorToolkitFactory.TranslateSectionCompsite_I(girderSet[0], dimensions[0],deckInfo, sectionIndex);

                    var element = new MctFrameElement()
                    {
                        Id = elementIndex++,
                        Mat = mat.Where(m => m.Type == MctMaterialTypeEnum.STEEL).FirstOrDefault(),
                        Sec = sec,
                        Type = MctElementTypeEnum.BEAM,
                        Node1 = nodes[i],
                        Node2 = nodes[i + 1],
                    };
                    _mctStore.AddElement(element);

                    var bearingPair = Bearings.Where(b => b.ObjectPlacement is IIfcLinearPlacement lp &&
                    (lp.Distance.DistanceAlong == currentPos)).ToList();
                    for(int j=0;j<bearingPair.Count;j++)
                    {
                        foreach(var node in nodes)
                        {
                            if(node.Id==(j * nodeNums + i + 1))
                                bearingMap[node] = bearingPair[j];
                        }    
                    }

                    bearingPair = Bearings.Where(b => b.ObjectPlacement is IIfcLinearPlacement lp &&
                    (lp.Distance.DistanceAlong == nextPos)).ToList();
                    for (int j = 0; j < bearingPair.Count; j++)
                    {
                        foreach (var node in nodes)
                        {
                            if (node.Id == (j * nodeNums + i + 2))
                                bearingMap[node] = bearingPair[j];
                        }
                    }
                }
            }
            var section = TranslatorToolkitFactory.TranslateSectionCompsite_I(girderSet[0], dimensions[0], deckInfo, sectionIndex);
            _mctStore.AddSection(section, ref sectionIndex);

            foreach (var pair in bearingMap)
            {
                var constraint = TranslatorToolkitFactory.TranslateBearing(pair.Value);
                _mctStore.AddSupport(pair.Key, constraint);
            }
        }

        public void WriteMctFile(string path)
        {
            if(File.Exists(path))
            {
                Console.WriteLine($"Warning: operation will overwrite the existing file {path}");
            }
            string dir = new FileInfo(path).Directory.FullName;
            if (!Directory.Exists(dir))
                throw new ArgumentException($"Directory {dir} doesn't exist");
            _mctStore.WriteMctFile(path);
        }

        private void GetLateralGirderNums(int girderNums)
        {
            var girderSet = _ifcModel.Instances.OfType<IIfcElementAssembly>().Where
                (ea => ea.PredefinedType == IfcElementAssemblyTypeEnum.USERDEFINED && ea.Name == "LateralGirder").ToList();
            _lateralBeamNums = girderSet.Count/(girderNums-1);
        }

        private void PreProcessDirectrix(IIfcElementAssembly girder)
        {
            var plateAssemblies = girder.IsDecomposedBy.FirstOrDefault().RelatedObjects
                .Where(o => o is IIfcPlate p && (p.ObjectType == "FLANGE-PLATE" || p.ObjectType == "WEB-PLATE"));
            foreach (var plate in plateAssemblies)
                PreProcessDirectrix((IIfcPlate)plate);
        }
        private void PreProcessDirectrix(IIfcBuildingElement linearBuildingElement)
        {
            var sectionedSolid = linearBuildingElement.Representation.Representations[0].Items.
                Where(i => i is IIfcSectionedSolidHorizontal).
                Select(i => (IIfcSectionedSolidHorizontal)i).FirstOrDefault();
            int id = sectionedSolid.Directrix.EntityLabel;
            double startdist = sectionedSolid.CrossSectionPositions[0].DistanceAlong;
            double enddist = sectionedSolid.CrossSectionPositions[1].DistanceAlong;
            double gap = (enddist - startdist) * 1.0 / (_lateralBeamNums - 1);
            for(int i=0;i<_lateralBeamNums;i++)
            {
                PushPosition(id, startdist + gap * i);
            }
        }
        private void PushPosition(int id,double dist,bool isSectionChanged=false)
        {
            if (!_pointPlacementTable[id].ContainsKey(dist))
                _pointPlacementTable[id].Add(dist, isSectionChanged);
        }

        public void TranslateLateralBeam()
        {
            var LateralBeamSet = _ifcModel.Instances.OfType<IIfcElementAssembly>()
                .Where(ea => (ea.PredefinedType == IfcElementAssemblyTypeEnum.USERDEFINED&&ea.Name=="LateralGirder")).ToList();
            if (LateralBeamSet.Count == 0)
                throw new InvalidOperationException("Must input Beams");

            List<IIfcMaterialSelect> BeamSetMaterial = new List<IIfcMaterialSelect>();
            foreach(var lateralBeam in LateralBeamSet)
            {
                BeamSetMaterial.Add(lateralBeam.Material);
            }
            var mat = TranslatorToolkitFactory.TranslateMaterial(BeamSetMaterial, ref matIndex);
            _mctStore.AddMaterial(mat);

            nodeIndex = (nodeIndex - 1) * _lateralBeamNums + 1;
            var offsetValues = TranslatorToolkitFactory.ParseOffsetValues(LateralBeamSet, _lateralBeamNums);

            PushLateralBeamNodes(offsetValues,ref nodeIndex);

            var sectionDimensions = TranslatorToolkitFactory.ParseLateralSection(LateralBeamSet[0]);
            var sec = new MctSectionDBUSER(MctSectionDBUSERShapeTypeEnum.DOUBLE_L, sectionDimensions,ref sectionIndex);
            _mctStore.AddSection(sec,ref sectionIndex);
            var matSet = _mctStore.GetTotalMaterial();
            var beamMat = matSet.Where(m => m.Value.Type == MctMaterialTypeEnum.STEEL).FirstOrDefault();

            for (int i=0;i<(LateralBeamSet.Count/_lateralBeamNums);i++)
            {
                for(int j=0;j<_lateralBeamNums;j++)
                {
                    var element = new MctFrameElement()
                    {
                        Id = elementIndex++,
                        Mat = beamMat.Value,
                        Sec = sec,
                        Type = MctElementTypeEnum.BEAM,
                        Node1 = LateralBeamNodeSet[i * _lateralBeamNums + j],
                        Node2 = LateralBeamNodeSet[i * _lateralBeamNums + j + _lateralBeamNums]
                    };
                    _mctStore.AddElement(element);
                    element = new MctFrameElement()
                    {
                        Id = elementIndex++,
                        Mat = beamMat.Value,
                        Sec = sec,
                        Type = MctElementTypeEnum.BEAM,
                        Node1 = LateralBeamNodeSet[i * _lateralBeamNums + j],
                        Node2 = LateralBeamNodeSet[i * _lateralBeamNums + j + 2*(LateralBeamSet.Count+_lateralBeamNums)],
                    };
                    _mctStore.AddElement(element);
                    element = new MctFrameElement()
                    {
                        Id = elementIndex++,
                        Mat = beamMat.Value,
                        Sec = sec,
                        Type = MctElementTypeEnum.BEAM,
                        Node2 = LateralBeamNodeSet[i * _lateralBeamNums + j + 2 * (LateralBeamSet.Count + _lateralBeamNums)],
                        Node1 = LateralBeamNodeSet[i * _lateralBeamNums + j + _lateralBeamNums],
                    };
                    _mctStore.AddElement(element);
                    element = new MctFrameElement((180,0))
                    {
                        Id = elementIndex++,
                        Mat = beamMat.Value,
                        Sec = sec,
                        Type = MctElementTypeEnum.BEAM,
                        Node2 = LateralBeamNodeSet[i * _lateralBeamNums + j + (LateralBeamSet.Count + _lateralBeamNums)],
                        Node1 = LateralBeamNodeSet[i * _lateralBeamNums + j + 2 * (LateralBeamSet.Count + _lateralBeamNums)],
                    };
                    _mctStore.AddElement(element);
                    element = new MctFrameElement((180, 0))
                    {
                        Id = elementIndex++,
                        Mat = beamMat.Value,
                        Sec = sec,
                        Type = MctElementTypeEnum.BEAM,
                        Node2 = LateralBeamNodeSet[i * _lateralBeamNums + j + 2 * (LateralBeamSet.Count + _lateralBeamNums)],
                        Node1 = LateralBeamNodeSet[i * _lateralBeamNums + j + _lateralBeamNums + (LateralBeamSet.Count + _lateralBeamNums)],
                    };
                    _mctStore.AddElement(element);
                }
            }

            var elasticLinkCollector = new List<MctElasticLink>();
            var rigidLinkCollector = new List<MctRigidLink>();

            for(int i=0;i<= LateralBeamSet.Count / _lateralBeamNums;i++)
            {
                for(int j=0;j<_lateralBeamNums-1;j++)
                {
                    var link = new MctElasticLink(elasticLinkIndex++,LateralBeamNodeSet[i*_lateralBeamNums+j],
                        LateralBeamNodeSet[i * _lateralBeamNums + j+1]);
                    elasticLinkCollector.Add(link);
                    _mctStore.AddElasticLink(link);
                }
            }
            for(int i=0;i<girderNodeSet.Count;i++)
            {
                var subNodes = new List<MctNode>() { LateralBeamNodeSet[i], LateralBeamNodeSet[i + girderNodeSet.Count] };
                var link = new MctRigidLink(rigidLinkIndex++, girderNodeSet[i], subNodes);
                rigidLinkCollector.Add(link);
                _mctStore.AddRigidLink(link);
            }
            _mctStore.AddLink(rigidLinkCollector, elasticLinkCollector);
            //_mctStore.AddNode(BeamNodesSet);
        }

        private void PushLateralBeamNodes((double, double) offsetValues,ref int nodeIndex)
        {
            var baseNodeSet = _mctStore.GetGirderNodeSet();
            nodeIndex = baseNodeSet.Count + 1;
            //var nodes = new List<MctNode>();
            const int NODECONJUNCTIONNUMS = 2;
            double locZ=0;
            for (int i=0;i<NODECONJUNCTIONNUMS;i++)
            {
                if (i == 0)
                    locZ = offsetValues.Item1;
                else
                    locZ = offsetValues.Item2;
                foreach(var node in baseNodeSet)
                {
                    LateralBeamNodeSet.Add(new MctNode(nodeIndex++, node.Value.X, node.Value.Y, locZ));
                }
            }
            for(int i=1;i<=baseNodeSet.Count-_lateralBeamNums; i++)
            {
                LateralBeamNodeSet.Add(new MctNode(nodeIndex++, (baseNodeSet[i].X + baseNodeSet[i + _lateralBeamNums].X) / 2,
                    (baseNodeSet[i].Y + baseNodeSet[i + _lateralBeamNums].Y) / 2, locZ));
            }
            _mctStore.AddNode(LateralBeamNodeSet);
        }
    }
}
