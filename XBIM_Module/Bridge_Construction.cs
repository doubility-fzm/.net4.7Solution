using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.MaterialResource;
using Xbim.Ifc4.MeasureResource;
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.SharedBldgElements;
using Xbim.Ifc4.SharedComponentElements;
using Xbim.IO;
using StructureDesignModule;


namespace XBIM_Module
{
    public class Bridge_Construction : IDisposable
    {
        private readonly string _outputPath = "";
        //       private readonly string _projectName = "xx工程";
        private readonly string _bridgeName = "xx钢板梁桥";
        private readonly IfcStore _model;//using external Alignment model as refenrence

        //Constructors
        Bridge_Construction() : this("../../TestFiles/alignment.ifc", "../../TestFiles/aligment&construction.ifc")
        {
            //empty
        }

        public Bridge_Construction(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath))
            {
                var bridgeconstruction = new Create_Alignment(inputPath) { IsStraight = true };
                bridgeconstruction.Create();
            }
            //use readonly IfcStore _model(created or existed)
            _model = IfcStore.Open(inputPath);
            PlateThicknessLists = new Dictionary<int, List<(double distanceAlong, double thickness)>>();
            //BridgeConstructionCurve = new List<IfcCurve>();
            _outputPath = outputPath;
        }
        #region get offset curve
        //using the following arguments as AddBridgeAlinment function
        private double BridgeStart { get; set; }
        private double BridgeEnd { get; set; }
        private double VerticalOffsetValue { get; set; }
        private double LateralOffsetValue { get; set; }
        private CrossSection cross { get; set; }
        private longitudinal_lateral_connection longitudinal_param { get; set; }
        private List<List<(double, double)>> plateThicknessList { get; set; }

        //private IfcCurve BridgeConstructionCurve { get; set; }
        private List<IfcCurve> BridgeConstructionCurve { get; set; }

        public void setLongitudinal_lateral_connection(longitudinal_lateral_connection inputParam)
        {
            longitudinal_param = inputParam;
        }

        public void SetCrossSection(CrossSection inputCross)
        {
            cross = inputCross;
        }

        public void SetBridgeAlong(double start,double end, double verOffset, double latOffset)
        {
            BridgeStart = start;
            BridgeEnd = end;
            VerticalOffsetValue = verOffset;
            LateralOffsetValue = latOffset;
        }

        private double StartGap { get; set; }
        private double EndGap { get; set; }
        public void SetGaps(double startGap, double endGap)
        {
            StartGap = startGap;
            EndGap = endGap;
        }

        public void Set_Bridge_Construction_Parameter(double start, double end, double verOffset, double latOffset)
        {
            BridgeStart = start;
            BridgeEnd = end;
            VerticalOffsetValue = verOffset;
            LateralOffsetValue = latOffset;
        }

        public IfcOffsetCurveByDistances AddBridgeAlignment(IfcAlignment mainAlignment)
        {
            using (var txn = this._model.BeginTransaction("Add Offset Curve"))
            {
                var offsetCurve = this._model.Instances.New<IfcOffsetCurveByDistances>(cbd =>
                {
                    cbd.BasisCurve = mainAlignment.Axis;
                    cbd.OffsetValues.Add(toolkit_factory.MakeDistanceExpresstion(_model, BridgeStart, LateralOffsetValue, VerticalOffsetValue));
                    cbd.OffsetValues.Add(toolkit_factory.MakeDistanceExpresstion(_model, BridgeEnd, LateralOffsetValue, VerticalOffsetValue));
                    cbd.Tag = "sturcture position curve";
                });
                var offsetCurveSolid = toolkit_factory.CreateSolidShapeBaseOnCurve(_model, offsetCurve, 0, BridgeEnd - BridgeStart);
                toolkit_factory.SetSurfaceColor(_model, offsetCurveSolid, 0, 1, 1);
                mainAlignment.Representation.Representations.FirstOrDefault(r => r.RepresentationIdentifier == "Body").Items.Add(offsetCurveSolid);

                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "StructureLine", "OffsetCurves", offsetCurveSolid);
                mainAlignment.Representation.Representations.Add(shape);

                txn.Commit();
                return offsetCurve;
            }
        }
        #endregion

        #region initiate the document's coordinate

        private IfcCartesianPoint Origin3D { get; set; }
        private IfcDirection AxisX3D { get; set; }
        private IfcDirection AxisY3D { get; set; }
        private IfcDirection AxisZ3D { get; set; }
        private IfcAxis2Placement3D WCS { get; set; }
        private IfcCartesianPoint Origin2D { get; set; }
        private IfcDirection AxisX2D { get; set; }
        private IfcDirection AxisY2D { get; set; }
        private IfcAxis2Placement2D WCS2D { get; set; }
        private void InitWCS()
        {
            using (var txn = this._model.BeginTransaction("Initialise WCS"))
            {
                var context3D = this._model.Instances.OfType<IfcGeometricRepresentationContext>()
                .Where(c => c.CoordinateSpaceDimension == 3)
                .FirstOrDefault();
                if (context3D.WorldCoordinateSystem is IfcAxis2Placement3D wcs)
                {
                    WCS = wcs;
                    Origin3D = wcs.Location;
                    AxisZ3D = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    wcs.Axis = AxisZ3D;
                    AxisX3D = toolkit_factory.MakeDirection(_model, 1, 0, 0);
                    wcs.RefDirection = AxisX3D;
                    AxisY3D = toolkit_factory.MakeDirection(_model, 0, 1, 0);
                }

                var context2D = this._model.Instances.OfType<IfcGeometricRepresentationContext>()
                    .Where(c => c.CoordinateSpaceDimension == 2)
                    .FirstOrDefault();
                if (context2D.WorldCoordinateSystem is IfcAxis2Placement2D wcs2d)
                {
                    WCS2D = wcs2d;
                    Origin2D = wcs2d.Location;
                    AxisX2D = toolkit_factory.MakeDirection(_model, 1, 0);
                    wcs2d.RefDirection = AxisX2D;
                    AxisY2D = toolkit_factory.MakeDirection(_model, 0, 1);
                }

                txn.Commit();
            }
        }
        #endregion
        #region generate SteelGirder

        //the dictionary refer to the platecode 
        //consider the thickness may change along the bridge construction curve
        //using nuget tuplegroup
        private Dictionary<int, List<(double distanceAlong, double thickness)>> PlateThicknessLists { get; set; }

        //public List<(double distanceAlong, double thickness)> GetPlateThicknessLists(int plateCode, Dictionary<int, List<(double distanceAlong, double thickness)>> 
        //    plateThicknessList)
        //{
        //    var tmp = new List<(double distanceAlong, double thickness)> ();
        //    tmp = plateThicknessList[plateCode];
        //    return tmp;
        //}

        //using var bridgeStart as start position  defined in get offset curve
        //define SectionParams for user use
        private List<double> SectionParams { get; set; }
        private List<List<(double, double)>> plateThicknessLists { get; set; }

        public void SetOverallSection(List<double> parameters)
        {
            if (parameters.Count == 3)
                SectionParams = parameters;
            else
            {
                throw new ArgumentException("The overall section dimensions should be {B1, B2, H}");
            }
        }

        //public Dictionary<int,List<(double distanceAlong,double thickness)>> CreatePlateThicknessAlongDistance(int distanceAlongStart,
        //    int distanceAlongEnd,List<double> thicknessLists)
        //{
        //    //Create PlateListsThickness also 0 for upper,1for web,2 for lower
        //    var PlateListsThickness = new Dictionary<int, List<(double distanceAlong, double thickness)>>();
        //    for(int i=0;i<3;i++)
        //    {
        //        var PairItem = new List<(double distanceAlong, double thickness)>();
        //        PairItem.Add((distanceAlongStart, thicknessLists[i]));
        //        PairItem.Add((distanceAlongEnd, thicknessLists[i]));
        //        PlateListsThickness[i]=PairItem;
        //    }
        //    return PlateListsThickness;
        //}

        //public void CreatePlateList(double endGap, CrossSection cross)
        //{
        //    var thickness = cross.GetThickness();
        //    plateThicknessList = new List<List<(double, double)>>()
        //    {
        //         new List<(double, double)>(){(endGap, thickness[0]) },
        //         new List<(double, double)>(){(endGap, thickness[1]) },
        //         new List<(double, double)>(){(endGap, thickness[2]) }
        //    };

        //    if (plateThicknessList.Count == 3)
        //    {
        //        //SetPlateThicknessLists(0, thicknessList[0]);
        //        //SetPlateThicknessLists(1, thicknessList[1]);
        //        //SetPlateThicknessLists(2, thicknessList[2]);
        //        PlateThicknessLists[0] = plateThicknessList[0];
        //        PlateThicknessLists[1] = plateThicknessList[1];
        //        PlateThicknessLists[2] = plateThicknessList[2];
        //    }
        //}
        //public void SetThickness(List<List<(double distAlong, double thickness)>> thicknessList)
        //{
        //    if (thicknessList.Count == 3)
        //    {
        //        SetPlateThicknessLists(0, thicknessList[0]);
        //        SetPlateThicknessLists(1, thicknessList[1]);
        //        SetPlateThicknessLists(2, thicknessList[2]);
        //    }
        //    else
        //        throw new ArgumentException("You have to provide thickness tables for top flange, web and bottom flange");
        //}

        //public void SetPlateThicknessLists(int plateCode, List<(double dist, double thickness)> thicknessList)
        //{
        //    if (plateCode > 3 || plateCode < 0)
        //        throw new ArgumentException("plateCode must be either of 0, 1, 2");
        //    PlateThicknessLists[plateCode]=thicknessList;
        //}

        public void SetThickness(List<List<(double distAlong, double thickness)>> thicknessList)
        {
            if(thicknessList.Count==3)
            {
                plateThicknessList = thicknessList;
            }
            else
                throw new ArgumentException("You have to provide thickness tables for top flange, web and bottom flange");
        }

        public IfcElementAssembly CreateIGirder(IfcCurve diretrix, List<List<(double distAlong, double thickness)>> thicknessList
            ,List<double> parameters)
        {
            using (var txn = this._model.BeginTransaction("Create Steel Girder"))
            {
                var girder = this._model.Instances.New<IfcElementAssembly>(elem =>
                {
                    elem.Name = _bridgeName;
                    elem.Description = "SteelGirder";
                    elem.PredefinedType = IfcElementAssemblyTypeEnum.GIRDER;
                });              

                var relAggregates = this._model.Instances.New<IfcRelAggregates>();
                relAggregates.RelatingObject = girder;

                var upperplate = this._model.Instances.New<IfcPlate>();
                upperplate.Name = "顶板";
                upperplate.ObjectType = "FLANGE-PLATE";

                var upperSolid = this._model.Instances.New<IfcSectionedSolidHorizontal>();
                upperSolid.Directrix = diretrix;
                var upperP1 = toolkit_factory.MakeCartesianPoint(_model, - parameters[0] / 2, 0);
                var upperP2 = toolkit_factory.MakeCartesianPoint(_model, parameters[0] / 2, 0);
                var upperLine = toolkit_factory.MakePolyLine(_model, new List<IfcCartesianPoint>() { upperP1, upperP2 });
                var upperProfile = toolkit_factory.MakeCenterLineProfile(_model, upperLine, thicknessList[0][0].thickness);
                upperSolid.CrossSections.Add(upperProfile);
                upperSolid.CrossSections.Add(upperProfile);
                var upperPos1 = toolkit_factory.MakeDistanceExpresstion(_model, thicknessList[0][0].distAlong);
                var upperPos2 = toolkit_factory.MakeDistanceExpresstion(_model, thicknessList[0][1].distAlong);
                upperSolid.CrossSectionPositions.Add(upperPos1);
                upperSolid.CrossSectionPositions.Add(upperPos2);

                toolkit_factory.SetSurfaceColor(_model, upperSolid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var upperShape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", upperSolid);

                upperplate.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(upperShape));
                upperplate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;

                relAggregates.RelatedObjects.Add(upperplate);

                var webPlate= this._model.Instances.New<IfcPlate>();
                webPlate.Name = "腹板";
                webPlate.ObjectType = "WEB-PLATE";

                var webSolid = this._model.Instances.New<IfcSectionedSolidHorizontal>();
                webSolid.Directrix = diretrix;
                var webP1 = toolkit_factory.MakeCartesianPoint(_model, 0, 0);
                var webP2 = toolkit_factory.MakeCartesianPoint(_model, 0, -parameters[2]);
                var webLine = toolkit_factory.MakePolyLine(_model, new List<IfcCartesianPoint>() { webP1, webP2 });
                var webProfile = toolkit_factory.MakeCenterLineProfile(_model, webLine, thicknessList[2][0].thickness);
                webSolid.CrossSections.Add(webProfile);
                webSolid.CrossSections.Add(webProfile);
                var webPos1 = toolkit_factory.MakeDistanceExpresstion(_model, thicknessList[2][0].distAlong);
                var webPos2 = toolkit_factory.MakeDistanceExpresstion(_model, thicknessList[2][1].distAlong);
                webSolid.CrossSectionPositions.Add(webPos1);
                webSolid.CrossSectionPositions.Add(webPos2);

                toolkit_factory.SetSurfaceColor(_model, webSolid, 24.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var webShape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", webSolid);

                webPlate.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(webShape));
                webPlate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;

                relAggregates.RelatedObjects.Add(webPlate);

                var lowerPlate = this._model.Instances.New<IfcPlate>();
                lowerPlate.Name = "底板";
                lowerPlate.ObjectType = "FLANGE-PLATE";

                var lowerSolid = this._model.Instances.New<IfcSectionedSolidHorizontal>();
                lowerSolid.Directrix = diretrix;
                var lowerP1 = toolkit_factory.MakeCartesianPoint(_model, -parameters[1] / 2, -parameters[2]);
                var lowerP2 = toolkit_factory.MakeCartesianPoint(_model, parameters[1] / 2, -parameters[2]);
                var lowerLine = toolkit_factory.MakePolyLine(_model, new List<IfcCartesianPoint>() { lowerP1, lowerP2 });
                var lowerLineProf = toolkit_factory.MakeCenterLineProfile(_model, lowerLine, thicknessList[1][0].thickness);
                lowerSolid.CrossSections.Add(lowerLineProf);
                lowerSolid.CrossSections.Add(lowerLineProf);
                var lowerPos1 = toolkit_factory.MakeDistanceExpresstion(_model, thicknessList[1][0].distAlong);
                var lowerPos2 = toolkit_factory.MakeDistanceExpresstion(_model, thicknessList[1][1].distAlong);
                lowerSolid.CrossSectionPositions.Add(lowerPos1);
                lowerSolid.CrossSectionPositions.Add(lowerPos2);

                toolkit_factory.SetSurfaceColor(_model, lowerSolid, 24.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var lowerShape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", lowerSolid);

                lowerPlate.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(lowerShape));
                lowerPlate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;

                relAggregates.RelatedObjects.Add(lowerPlate);

                txn.Commit();

                return girder;
            }
        }

        private IfcElementAssembly CreateSteelGirder(IfcCurve directrix)
        {
            using (var txn = this._model.BeginTransaction("Create SteelGirder"))
            {
                var girder = this._model.Instances.New<IfcElementAssembly>(elem =>
                {
                    elem.Name = _bridgeName;
                    elem.Description = "SteelGirder";
                    elem.PredefinedType = IfcElementAssemblyTypeEnum.GIRDER;
                });

                //暂时不添加材料特性
                // 0 for upperflange,1 for web, 2 for lower flange
                var platesCodes = new List<int>() { 0, 1, 2 };
                var plates = platesCodes.Select(c => CreateIPlate(directrix, c)).ToList();

                //加劲和横隔板暂时不添加

                //Aggregate flanges and webs into girder assembly
                var relAggregates = this._model.Instances.New<IfcRelAggregates>(r =>
                {
                    r.RelatingObject = girder;
                    foreach(var plate in plates)
                    {
                        r.RelatedObjects.Add(plate);
                    }
                });
                txn.Commit();
                return girder;
            }
        }

        private IfcElementAssembly CreateIPlate(IfcCurve diretrix, int plateCode)
        {
            var plateAssembly = this._model.Instances.New<IfcElementAssembly>(IEA =>
            {
                IEA.Name = plateCode == 0 ? "顶板" : (plateCode == 2 ? "腹板" : "底板");
                IEA.ObjectType = (plateCode == 0 || plateCode == 1) ? "FLANGE-ASSEMBLY" : "WEB-ASSEMBLY";
                IEA.PredefinedType = IfcElementAssemblyTypeEnum.USERDEFINED;
            });
            //define a relAggregates to represente the plate assembly
            var relAggregate = this._model.Instances.New<IfcRelAggregates>(rg => rg.RelatingObject = plateAssembly);

            //information for location
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0, t = 0, offLateral = 0, offVertical = 0;

            var thicknessList = PlateThicknessLists[plateCode];
            var StartDistanceAlong = StartGap;

            for (int i = 0; i < thicknessList.Count; ++i)
            {
                var start = StartDistanceAlong;
                var end = thicknessList[i].distanceAlong-80;
                var parametersNames = new List<string>() { "B1", "B2", "H" };
                var parameters = new Dictionary<string, double>();
                for (int j = 0; j < SectionParams.Count; j++)
                {
                    parameters[parametersNames[j]] = SectionParams[j];
                }

                switch (plateCode)
                {
                    case 0:
                        x1 = -parameters["B1"] / 2;
                        y1 = -thicknessList[i].thickness / 2;
                        x2 = -x1;
                        y2 = y1;
                        t = thicknessList[i].thickness;
                        break;
                    case 2:
                        x1 = 0;
                        y1 = 0;
                        x2 = -parameters["H"];
                        y2 = 0;
                        t = thicknessList[i].thickness;
                        break;
                    case 1:
                        x1 = parameters["B2"] / 2;
                        y1 = -parameters["H"];
                        x2 = -x1;
                        y2 = y1;
                        t = thicknessList[i].thickness;
                        break;
                    default:
                        break;
                }
                var solid = this._model.Instances.New<IfcSectionedSolidHorizontal>(ssh =>
                {
                    ssh.Directrix = diretrix;
                    var p1 = toolkit_factory.MakeCartesianPoint(_model, x1, y1);
                    var p2 = toolkit_factory.MakeCartesianPoint(_model, x2, y2);
                    var line = toolkit_factory.MakePolyLine(_model, new List<IfcCartesianPoint>() { p1, p2 });
                    var profile = toolkit_factory.MakeCenterLineProfile(_model, line, t);
                    ssh.CrossSections.Add(profile);
                    ssh.CrossSections.Add(profile);
                    var posStart = toolkit_factory.MakeDistanceExpresstion(_model, start, offLateral, offVertical);
                    var posEnd = toolkit_factory.MakeDistanceExpresstion(_model, end, offLateral, offVertical);
                    ssh.CrossSectionPositions.Add(posStart);
                    ssh.CrossSectionPositions.Add(posEnd);
                });
                //now the geometry data already
                //using this data to rendering
                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweepSolid", solid);
                //update the startDistanceAlong
                StartDistanceAlong = end;

                var plate = this._model.Instances.New<IfcPlate>(p =>
                {
                    p.Name = $"{plateAssembly.Name}-0{i + 1}";
                    p.ObjectType = (plateCode == 0 || plateCode == 1) ? "FLANGE-PLATE" : "WEB-PLATE";
                    p.Representation = this._model.Instances.New<IfcProductDefinitionShape>(
                        pd => pd.Representations.Add(shape));
                    p.PredefinedType = IfcPlateTypeEnum.USERDEFINED;
                });

                relAggregate.RelatedObjects.Add(plate);
            }

            return plateAssembly;
        }

        #endregion

        #region Add bearing 
        //declare bearingtypetable
        //if using another bearing type just addin below table
        private readonly Dictionary<string, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)> bearingTypeEnum =
            new Dictionary<string, (bool fixedLateral, bool fixedLongitudinal, bool fixedVertical)>()
            {
                {"双向固定支座",(true,true,false)},
                {"单向滑动支座",(true,false,false) }
            };
        private List<(double distAlong,double offsetVertical,string bearingtype)> bearingInfoList { get; set; }

        public void SetBearingInfoList(List<(double distAlong, double offsetVertical, string bearingtype)> sourceInfo)
        {
            bearingInfoList = sourceInfo;
        }
        private IfcShapeRepresentation CreateBearingShape()
        {
            var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "CSG");
            var rectangle = toolkit_factory.MakeRectangleProfile(_model, 940, 920);
            var vzNegated = toolkit_factory.MakeDirection(_model, 0, 0, -1);
            var pos1 = toolkit_factory.MakeAxis2Placement3D(_model);
            shape.Items.Add(toolkit_factory.MakeExtrudedAreaSolid(_model, rectangle, pos1, vzNegated, 50));

            var rectangle2 = toolkit_factory.MakeRectangleProfile(_model, 80, 920);
            var pos4 = toolkit_factory.MakeAxis2Placement3D(_model, toolkit_factory.MakeCartesianPoint(_model, -410, 0, -50));
            shape.Items.Add(toolkit_factory.MakeExtrudedAreaSolid(_model, rectangle2, pos4, vzNegated, 80));

            var pos5 = toolkit_factory.MakeAxis2Placement3D(_model, toolkit_factory.MakeCartesianPoint(_model, 410, 0, -50));
            shape.Items.Add(toolkit_factory.MakeExtrudedAreaSolid(_model, rectangle2, pos5, vzNegated, 80));

            var rectangle3 = toolkit_factory.MakeRectangleProfile(_model, 740, 850);
            var pos6 = toolkit_factory.MakeAxis2Placement3D(_model, toolkit_factory.MakeCartesianPoint(_model, 0, 0, -50));
            var solid = toolkit_factory.MakeExtrudedAreaSolid(_model, rectangle3, pos6, vzNegated, 70);
            toolkit_factory.SetSurfaceColor(_model, solid, 1, 0, 0);
            shape.Items.Add(solid);

            var circle = toolkit_factory.MakeCircleProfile(_model, 300);
            var pos2 = toolkit_factory.MakeAxis2Placement3D(_model, toolkit_factory.MakeCartesianPoint(_model, 0, 0, -120));
            shape.Items.Add(toolkit_factory.MakeExtrudedAreaSolid(_model, circle, pos2, vzNegated, 65));

            var rectangle4 = toolkit_factory.MakeRectangleProfile(_model, 800, 1200);
            var pos3 = toolkit_factory.MakeAxis2Placement3D(_model, toolkit_factory.MakeCartesianPoint(_model, 0, 0, -185));
            shape.Items.Add(toolkit_factory.MakeExtrudedAreaSolid(_model, rectangle4, pos3, vzNegated, 75));

            return shape;
        }
        private IfcProxy CreateBearing(IfcCurve bridgeAlign,
            (double distAlong, double offsetVertical, string bearingtype) bearingInfoList)
        {
            using (var txn = this._model.BeginTransaction("Create bearing"))
            {
                var shape = CreateBearingShape();
                var distance = toolkit_factory.MakeDistanceExpresstion(_model, bearingInfoList.distAlong, 0, bearingInfoList
                    .offsetVertical);
                var bearing = this._model.Instances.New<IfcProxy>();
                bearing.Name = bearingInfoList.bearingtype;
                bearing.Description = "IfcBearing";
                bearing.ProxyType = IfcObjectTypeEnum.PRODUCT;
                bearing.ObjectType = "POT";
                bearing.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));

                bearing.ObjectPlacement = toolkit_factory.MakeLinearPlacement(_model, bridgeAlign,
                    distance);
                var pset_BearingCommon = _model.Instances.New<IfcPropertySet>(ps =>
                  {
                      ps.Name = "Pset_BearingCommon";
                      var displacementAccomodated = _model.Instances.New<IfcPropertyListValue>(l =>
                        {
                            l.Name = "DisplacementAccomodated";
                            l.ListValues.Add(new IfcBoolean(!bearingTypeEnum[bearingInfoList.bearingtype].fixedLongitudinal));
                            l.ListValues.Add(new IfcBoolean(!bearingTypeEnum[bearingInfoList.bearingtype].fixedLateral));
                            l.ListValues.Add(new IfcBoolean(!bearingTypeEnum[bearingInfoList.bearingtype].fixedVertical));
                        });
                      var rotationAccomodated = _model.Instances.New<IfcPropertyListValue>(l =>
                        {
                            l.Name = "RotationAccomodated";
                            l.ListValues.Add(new IfcBoolean(true));
                            l.ListValues.Add(new IfcBoolean(true));
                            l.ListValues.Add(new IfcBoolean(true));
                        });

                      ps.HasProperties.Add(displacementAccomodated);
                      ps.HasProperties.Add(rotationAccomodated);
                  });

                this._model.Instances.New<IfcRelDefinesByProperties>(r =>
                {
                    r.RelatingPropertyDefinition = pset_BearingCommon;
                    r.RelatedObjects.Add(bearing);
                });
                txn.Commit();
                return bearing;
            }
        }
        #endregion
        #region using all of functions in this class to generate the bridge     
        public void build()
        {
            InitWCS();
            var site = _model.Instances.OfType<IfcSite>().FirstOrDefault();
            if (site == null)
                throw new NotSupportedException("Input IFC file must include an instance of IfcSite");
            var alignment = _model.Instances.OfType<IfcAlignment>().FirstOrDefault();
            if (alignment == null)
                throw new NotSupportedException("Input IFC file must include an instance of IfcAlignment");
            //Create BridgeAlignment
            BridgeConstructionCurve = new List<IfcCurve>();

            //Create Girder
            for (int girdercount = 0; girdercount < cross.lateral_offset_dis.Length; girdercount++)
            {
                Set_Bridge_Construction_Parameter(BridgeStart, BridgeEnd, cross.vertical_offset_dis * -1, cross.lateral_offset_dis[girdercount] * 1000);
                BridgeConstructionCurve.Add(AddBridgeAlignment(alignment));
            }

            Set_Bridge_Construction_Parameter(BridgeStart, BridgeEnd, 0, 0);
            var align = AddBridgeAlignment(alignment);
            var Deck = CreateBridgeDeck(align);
            toolkit_factory.AddPrductIntoSpatial(_model, site, Deck, "Add BridgeDeck to site");

            for (int i=0;i<BridgeConstructionCurve.Count;i++)
            {
                var girder = CreateIGirder(BridgeConstructionCurve[i], plateThicknessList, SectionParams);
                CreateMaterialForGirder(girder);
                toolkit_factory.AddPrductIntoSpatial(_model, site, girder, "Add Girder to site");
            }
            //toolkit_factory.AddPrductIntoSpatial(_model, site, plate, "Add plate to site");
            //generate STARTGAPBearings
            //var bearing = CreateBearing(BridgeConstructionCurve[0], (40, -1915, "双向固定支座"));
            //toolkit_factory.AddPrductIntoSpatial(_model, site, bearing, "Add Bearing to site");
            //generate ENDGAPBearings
            for (int i=0;i<BridgeConstructionCurve.Count;i++)
            {
                var bearing1 = CreateBearing(BridgeConstructionCurve[i], (StartGap, -(cross.girder_web_height*1000+cross.girder_web_thickness*1000/2), "双向固定支座"));
                var bearing2 = CreateBearing(BridgeConstructionCurve[i], (BridgeEnd - BridgeStart - EndGap, -(cross.girder_web_height * 1000 + cross.girder_web_thickness * 1000 / 2), "单向滑动支座"));
                toolkit_factory.AddPrductIntoSpatial(_model, site, bearing1, "Add Bearing to site");
                toolkit_factory.AddPrductIntoSpatial(_model, site, bearing2, "Add Bearing to site");
            }

            //Create lateralgirder
            double distAlong = StartGap;
            for(int j=0;j<longitudinal_param.Getintermediate_beam_nums();j++)
            {
                distAlong = longitudinal_param.Getlateral_beam_gap() * j * 1000;
                for (int i = 0; i < BridgeConstructionCurve.Count - 1; i++)
                {
                    var connectPlate = CreateLateralConnection(BridgeConstructionCurve[i], BridgeConstructionCurve[i + 1], distAlong);
                    toolkit_factory.AddPrductIntoSpatial(_model, site, connectPlate, "Add connectPlate");

                    var connectPlateGirder = CreateLateralGirder(BridgeConstructionCurve[i], BridgeConstructionCurve[i + 1], distAlong);
                    CreateMaterialForGirder(connectPlateGirder);
                    toolkit_factory.AddPrductIntoSpatial(_model, site, connectPlateGirder, "Add connectPlateGirder");
                }
            }

            //添加纵向加劲肋
            for (int j = 0; j < longitudinal_param.Getintermediate_beam_nums() - 1; j++)
            {
                distAlong = longitudinal_param.Getlateral_beam_gap() * j * 1000;
                for (int i = 0; i < BridgeConstructionCurve.Count; i++)
                {
                    int index = 1;
                    if (i >= (BridgeConstructionCurve.Count / 2))
                        index = -1;
                    for (int k = 0; k < cross.stiffener_vertical_postion.Count; k++)
                    {
                        //增加一个判断，当k=0,且偏移为0的时候需要跳过
                        if (k == 0 && cross.stiffener_vertical_postion[0] == 0)
                            continue;
                        var stiffener = CreateLongitudinalStiffener(BridgeConstructionCurve[i],
                            distAlong + 40, distAlong + longitudinal_param.Getlateral_beam_gap() * 1000 - 40, k, index);
                        toolkit_factory.AddPrductIntoSpatial(_model, site, stiffener, "Add longditudinal stiffener");
                    }
                }
            }
            //最外层循环是以纵向数量循环
            for(int j=0;j<BridgeConstructionCurve.Count;j++)
            {
                int index = 1;
                if (j >= (BridgeConstructionCurve.Count / 2))
                    index = -1;
                //这层循环是每一段纵向加劲肋上布置情况
                for (int k=0;k<longitudinal_param.Getintermediate_beam_nums()-1;k++)
                {
                    //每一span/nums之间的横向加劲肋布置
                    for(int m=0;m<longitudinal_param.stiffener_nums;m++)
                    {
                        var lateral_stiffener = CreateLateralStiffener(BridgeConstructionCurve[j], StartGap +k*
                            longitudinal_param.Getlateral_beam_gap() * 1000 + m * longitudinal_param.stiffener_gap*1000,index);
                        toolkit_factory.AddPrductIntoSpatial(_model, site, lateral_stiffener, "Add lateral stiffener");
                    }
                }
            }
            //var lateral_stiffener = CreateLateralStiffener(BridgeConstructionCurve[0], 20);
            //toolkit_factory.AddPrductIntoSpatial(_model, site, lateral_stiffener, "Add lateral stiffener");

            _model.SaveAs(_outputPath, StorageType.Ifc);
        }
        #endregion

        public void buildDeck()
        {
            InitWCS();
            var site = _model.Instances.OfType<IfcSite>().FirstOrDefault();
            if (site == null)
                throw new NotSupportedException("Input IFC file must include an instance of IfcSite");
            var alignment = _model.Instances.OfType<IfcAlignment>().FirstOrDefault();
            if (alignment == null)
                throw new NotSupportedException("Input IFC file must include an instance of IfcAlignment");

            Set_Bridge_Construction_Parameter(BridgeStart, BridgeEnd, cross.vertical_offset_dis * -1, cross.lateral_offset_dis[4] * 1000);
            //Set_Bridge_Construction_Parameter(BridgeStart, BridgeEnd, 0, 0);
            var align1 = AddBridgeAlignment(alignment);
            Set_Bridge_Construction_Parameter(BridgeStart, BridgeEnd, cross.vertical_offset_dis * -1, cross.lateral_offset_dis[5] * 1000);
            var align2 = AddBridgeAlignment(alignment);
            Set_Bridge_Construction_Parameter(BridgeStart, BridgeEnd, cross.vertical_offset_dis * -1, cross.lateral_offset_dis[3] * 1000);
            var align3 = AddBridgeAlignment(alignment);
            //var connectPlate = CreateLGirderPlate(align);
            //toolkit_factory.AddPrductIntoSpatial(_model, site, connectPlate, "Add something");

            //var Deck = CreateBridgeDeck(align);
            //toolkit_factory.AddPrductIntoSpatial(_model, site, Deck, "Add BridgeDeck to site");
            //var bearing = CreateBearing(align, (BridgeEnd - BridgeStart - EndGap, -(cross.girder_web_height * 1000 + cross.girder_web_thickness * 1000 / 2), "单向滑动支座"));
            //toolkit_factory.AddPrductIntoSpatial(_model, site, bearing, "Add Bearing to site");
            //var girder = CreateIGirder(align, plateThicknessList, SectionParams);


            //var connectPlateGirder = CreateLateralGirder(align1, align2, StartGap);
            //toolkit_factory.AddPrductIntoSpatial(_model, site, connectPlateGirder, "Add something");

            //var stiffener = CreateLongitudinalStiffener(align1, 40, 500);
            //toolkit_factory.AddPrductIntoSpatial(_model, site, stiffener, "Add something");


            _model.SaveAs(_outputPath, StorageType.Ifc);
        }

        #region Add BridgeDeck
        private IfcSlab CreateBridgeDeck(IfcCurve diretrix)
        {
            using (var txn = this._model.BeginTransaction("CreateBridgeDeck"))
            {
                var slab = this._model.Instances.New<IfcSlab>();
                slab.Name = "BridgeDeck";
                slab.ObjectType = "ConcreteDeck";

                CreateMaterialForDeckPlate(slab);

                var solid = this._model.Instances.New<IfcSectionedSolidHorizontal>();
                solid.Directrix = diretrix;
                //var p1 = toolkit_factory.MakeCartesianPoint(_model, -200, 0);
                //var p2 = toolkit_factory.MakeCartesianPoint(_model, 200, 0);
                //var line = toolkit_factory.MakePolyLine(_model, new List<IfcCartesianPoint>() { p1, p2 });

                var profile = toolkit_factory.MakeBridgeDeckProfile(_model, cross);
                solid.CrossSections.Add(profile);
                solid.CrossSections.Add(profile);
                var pos1 = toolkit_factory.MakeDistanceExpresstion(_model, 40);
                var pos2 = toolkit_factory.MakeDistanceExpresstion(_model, 29960);
                solid.CrossSectionPositions.Add(pos1);
                solid.CrossSectionPositions.Add(pos2);

                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                slab.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                slab.PredefinedType = IfcSlabTypeEnum.USERDEFINED;

                txn.Commit();
                return slab;
            }
        }

        //private IfcSlab CreateBridgeDeck(IfcCurve diretrix)
        //{
        //    using (var txn = this._model.BeginTransaction("AddBridgeDeck"))
        //    {
        //        var BridgeDeck = this._model.Instances.New<IfcSlab>();
        //        BridgeDeck.Name = "BridgeDeck";
        //        BridgeDeck.ObjectType = "ConcreteDeck";

        //        var DeckSolid =this._model.Instances.New<IfcSectionedSolidHorizontal>();
        //        DeckSolid.Directrix = diretrix;

        //        //var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "CSG");
        //        //var DeckProfile = toolkit_factory.MakeBridgeDeckProfile(_model, cross);
        //        //var rectangle = toolkit_factory.MakeRectangleProfile(_model, 940, 920);
        //        //var vzNegated = toolkit_factory.MakeDirection(_model, 0, 0, -1);
        //        //var pos1 = toolkit_factory.MakeAxis2Placement3D(_model);
        //        //shape.Items.Add(toolkit_factory.MakeExtrudedAreaSolid(_model, DeckProfile, pos1, vzNegated, 50));

        //        var DeckProfile = toolkit_factory.MakeBridgeDeckProfile(_model, cross);
        //        DeckSolid.CrossSections.Add(DeckProfile);
        //        DeckSolid.CrossSections.Add(DeckProfile);
        //        var DeckSolidPos1 = toolkit_factory.MakeDistanceExpresstion(_model, 40);
        //        var DeckSolidPos2 = toolkit_factory.MakeDistanceExpresstion(_model, 29960);
        //        DeckSolid.CrossSectionPositions.Add(DeckSolidPos1);
        //        DeckSolid.CrossSectionPositions.Add(DeckSolidPos2);

        //        toolkit_factory.SetSurfaceColor(_model, DeckSolid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
        //        var DeckShape = toolkit_factory.MakeShapeRepresentation(_model, 3, "BridgeDeck", "AdvancedSweptSolid", DeckSolid);

        //        BridgeDeck.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(DeckShape));
        //        BridgeDeck.PredefinedType = IfcSlabTypeEnum.USERDEFINED;

        //        txn.Commit();
        //        return BridgeDeck;
        //    }
        //}
        #endregion
        public void Dispose()
        {
            _model.Dispose();
        }

        //测试函数 板单元
        private IfcPlate CreatePlateset(IfcCurve diretrix)
        {
            using (var txn = this._model.BeginTransaction("CreatePlate"))
            {
                var plate = this._model.Instances.New<IfcPlate>();
                plate.Name = "TestPlate";
                plate.ObjectType = "WEB_PLATE";

                var solid = this._model.Instances.New<IfcSectionedSolidHorizontal>();
                solid.Directrix = diretrix;
                //var p1 = toolkit_factory.MakeCartesianPoint(_model, -200, 0);
                //var p2 = toolkit_factory.MakeCartesianPoint(_model, 200, 0);
                //var line = toolkit_factory.MakePolyLine(_model, new List<IfcCartesianPoint>() { p1, p2 });

                //var profile = toolkit_factory.MakeBridgeDeckProfile(_model, cross);
                var profile=toolkit_factory.MakeLGirderProfile(_model, 10, 10, 170,0);
                solid.CrossSections.Add(profile);
                solid.CrossSections.Add(profile);
                var pos1 = toolkit_factory.MakeDistanceExpresstion(_model, 40);
                var pos2 = toolkit_factory.MakeDistanceExpresstion(_model, 29960);
                solid.CrossSectionPositions.Add(pos1);
                solid.CrossSectionPositions.Add(pos2);

                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid",solid);

                plate.Representation= this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                plate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;

                txn.Commit();
                return plate;
            }
        }

        private IfcPlate CreateLateralStiffener(IfcCurve diretrix, double start,int index=1)
        {
            using (var txn = this._model.BeginTransaction("Create Lateral Stiffener"))
            {
                var plate = this._model.Instances.New<IfcPlate>();
                plate.Name = "Stiffener";
                plate.ObjectType = "PLATE";

                var solid = this._model.Instances.New<IfcExtrudedAreaSolid>();
                var pointSet = new List<IfcCartesianPoint>();
                pointSet.Add(toolkit_factory.MakeCartesianPoint(_model));
                pointSet.Add(toolkit_factory.MakeCartesianPoint(_model, 0, -index*longitudinal_param.stiffener_info.width));
                pointSet.Add(toolkit_factory.MakeCartesianPoint(_model, longitudinal_param.stiffener_info.height - 80,
                    -index * longitudinal_param.stiffener_info.width));
                pointSet.Add(toolkit_factory.MakeCartesianPoint(_model,longitudinal_param.stiffener_info.height - 80));
                pointSet.Add(toolkit_factory.MakeCartesianPoint(_model));

                solid.SweptArea = toolkit_factory.MakeClosedProfile(_model, pointSet);

                var dist = toolkit_factory.MakeDistanceExpresstion(_model, start, 0, -cross.girder_upper_flange_thickness / 2.0 * 1000);
                var lp = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix, dist);
                var position = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, lp);

                solid.Position = position;
                solid.ExtrudedDirection= toolkit_factory.MakeDirection(_model, 0, 0, 1);
                solid.Depth = longitudinal_param.stiffener_info.thickness;

                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                plate.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                plate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;
                txn.Commit();
                return plate;
            }
        }

        private IfcPlate CreateLongitudinalStiffener(IfcCurve diretrix, double start, double end,int StiffenerIndex=0,int index=1)
        {
            using (var txn = this._model.BeginTransaction("CreatePlate"))
            {
                var plate = this._model.Instances.New<IfcPlate>();
                plate.Name = "LongitudinalStiffener";
                plate.ObjectType = "PLATE";

                var solid = this._model.Instances.New<IfcSectionedSolidHorizontal>();
                solid.Directrix = diretrix;

                var p1 = toolkit_factory.MakeCartesianPoint(_model, 0, 0);
                var p2 = toolkit_factory.MakeCartesianPoint(_model, index*cross.stiffener_info.width * 1000, 0);

                var line = toolkit_factory.MakePolyLine(_model, p1, p2);
                var profile = toolkit_factory.MakeCenterLineProfile(_model, line, cross.girder_web_thickness * 1000);

                solid.CrossSections.Add(profile);
                solid.CrossSections.Add(profile);

                var pos1 = toolkit_factory.MakeDistanceExpresstion(_model, start, 0, -cross.stiffener_vertical_postion[StiffenerIndex]);
                var pos2 = toolkit_factory.MakeDistanceExpresstion(_model, end, 0, -cross.stiffener_vertical_postion[StiffenerIndex]);
                solid.CrossSectionPositions.Add(pos1);
                solid.CrossSectionPositions.Add(pos2);

                toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
                var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

                plate.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
                plate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;

                txn.Commit();
                return plate;
            }
        }

        //测试函数 生成横梁连接板函数
        private IfcPlate CreateConnectPlate(double startDist,IfcCurve diretrix1,IfcCurve diretrix2, int typeId)
        {
            //using (var txn = this._model.BeginTransaction("CreateConnectPlate"))
            //{
            //    var plate = this._model.Instances.New<IfcPlate>();
            //    plate.Name = "ConnectPlate";
            //    plate.ObjectType = "PLATE";

            //    var solid = this._model.Instances.New<IfcExtrudedAreaSolid>();
            //    double TH = Math.Atan(0.75 * cross.girder_web_height / (Math.Abs(cross.girder_web_height / (cross.lateral_offset_dis[0]
            //        - cross.lateral_offset_dis[1]))));
            ////只有170时L角钢的肢长
            //    solid.SweptArea = toolkit_factory.MakeLateralConnectPlateProfile(_model,
            //        (cross.girder_upper_flange_width * 1000, 100, 40, 170, TH, typeId));

            //    if(typeId==0||typeId==3)
            //    {
            //        //暂时先取10，这个厚度为加劲厚度
            //        var distance = toolkit_factory.MakeDistanceExpresstion(_model, StartGap + 10, cross.girder_web_thickness / 2, -100);
            //        var positonPlacement = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix1, distance);
            //        var position = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, positonPlacement);
            //        solid.Position = position;
            //        solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
            //        solid.Depth = 10;
            //    }
            //    else
            //    {
            //        var distance = toolkit_factory.MakeDistanceExpresstion(_model, StartGap + 10, -cross.girder_web_thickness / 2, -100);
            //        var positonPlacement = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix2, distance);
            //        var position = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, positonPlacement);
            //        solid.Position = position;
            //        solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
            //        solid.Depth = 10;
            //    }

            //    toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
            //    var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

            //    plate.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
            //    plate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;
            ////var distance = toolkit_factory.MakeDistanceExpresstion(_model, 40, 5, -100);

            ////plate.ObjectPlacement = toolkit_factory.MakeLinearPlacement(_model, diretrix, distance);

            //txn.Commit();
            //return plate;
            //}
            var plate = this._model.Instances.New<IfcPlate>();
            plate.Name = "ConnectPlate";
            plate.ObjectType = "PLATE";

            var solid = this._model.Instances.New<IfcExtrudedAreaSolid>();
            double TH = Math.Atan((0.75 * cross.girder_web_height*1000-170-40-170/2) / (Math.Abs((cross.lateral_offset_dis[0]
                - cross.lateral_offset_dis[1]))*500));
            //只有170时L角钢的肢长
            solid.SweptArea = toolkit_factory.MakeLateralConnectPlateProfile(_model,
                (cross.girder_upper_flange_width * 1000, 100, 40, 170, TH, typeId));

            //var distance = new List<IfcDistanceExpression>();
            //var positionPlacement = this._model.Instances.New<IfcLinearPlacement>();
            //var position = this._model.Instances.New<IfcAxis2Placement3D>();
            //switch (typeId)
            //{
            //    case 0:
            //        distance[typeId] = toolkit_factory.MakeDistanceExpresstion(_model, StartGap + 10, cross.girder_web_thickness / 2, -100);
            //        positionPlacement= toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix1, distance[typeId]);
            //        //position = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, positionPlacement);
            //        break;
            //    case 1:
            //        distance[typeId] = toolkit_factory.MakeDistanceExpresstion(_model, StartGap + 10, -cross.girder_web_thickness / 2, -100);
            //        positionPlacement = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix2, distance[typeId]);
            //        break;
            //    case 2:
            //        distance[typeId] = toolkit_factory.MakeDistanceExpresstion(_model, StartGap + 10, -cross.girder_web_thickness / 2, -100 - cross.girder_web_height*0.75*1000);
            //        positionPlacement = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix1, distance[typeId]);
            //        break;
            //    case 3:
            //        distance[typeId] = toolkit_factory.MakeDistanceExpresstion(_model, StartGap + 10, -cross.girder_web_thickness / 2, -100 - cross.girder_web_height * 0.75 * 1000);
            //        positionPlacement = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix2, distance[typeId]);
            //        break;
            //    default:
            //        break;
            //}
            //var position = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, positionPlacement);
            //solid.Position = position;
            //solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
            //solid.Depth = 10;
            //这一段代码重复较多，最好用case的情况优化一下

            if (typeId == 0 || typeId == 3)
            {
                if(typeId==0)
                {
                    var distance = toolkit_factory.MakeDistanceExpresstion(_model, startDist + 10, cross.girder_web_thickness / 2, -100);
                    var positionPlacement = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix1, distance);
                    //暂时先取10，这个厚度为加劲厚度
                    var position = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, positionPlacement);
                    solid.Position = position;
                    solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    solid.Depth = 10;
                }
                else
                {
                    var distance = toolkit_factory.MakeDistanceExpresstion(_model, startDist + 10, -cross.girder_web_thickness / 2, -100 - cross.girder_web_height * 0.75 * 1000);
                    var positionPlacement = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix1, distance);
                    //暂时先取10，这个厚度为加劲厚度
                    var position = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, positionPlacement);
                    solid.Position = position;
                    solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    solid.Depth = 10;
                }
            }
            if (typeId == 1)
            {
                var distance = toolkit_factory.MakeDistanceExpresstion(_model, startDist + 10, -cross.girder_web_thickness / 2, -100);
                var positonPlacement = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix2, distance);
                var position = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, positonPlacement);
                solid.Position = position;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                solid.Depth = 10;
            }
            if(typeId == 2)
            {
                var distance = toolkit_factory.MakeDistanceExpresstion(_model, startDist + 10, -cross.girder_web_thickness / 2, -100 - cross.girder_web_height * 0.75 * 1000);
                var positonPlacement = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix2, distance);
                var position = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, positonPlacement);
                solid.Position = position;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                solid.Depth = 10;
            }
            if(typeId == 4)
            {
                var distance1 = toolkit_factory.MakeDistanceExpresstion(_model, startDist + 10, 0, -100 - 0.75 * cross.girder_web_height * 1000);
                var distance2 = toolkit_factory.MakeDistanceExpresstion(_model, startDist + 10, 0, -100 - 0.75 * cross.girder_web_height * 1000);
                var linearPlacement1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distance1);
                var point1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement1);
                var linearPlacement2 = toolkit_factory.MakeLinearPlacement(_model, diretrix2, distance2);
                var point2 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement2);

                var distance = toolkit_factory.MakeDistanceExpresstion(_model, startDist + 10, GetLength(point2, point1)/2, -100 - 0.75 * cross.girder_web_height * 1000);
                var linearPlacement=toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix1, distance);
                var locPlacement = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, linearPlacement);
                //var distance = toolkit_factory.MakeDistanceExpresstion(_model, StartGap + 10, -cross.girder_web_thickness / 2, -100 - cross.girder_web_height * 0.75 * 1000);
                //var positonPlacement = toolkit_factory.MakeLinearPlacement_LateralConnectPlate(_model, diretrix2, distance);
                //var position = toolkit_factory.ToAixs3D_LateralConnectPlate(_model, positonPlacement);
                solid.Position = locPlacement;
                solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                solid.Depth = 10;
            }

            toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
            var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

            plate.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
            plate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;
            //var distance = toolkit_factory.MakeDistanceExpresstion(_model, 40, 5, -100);

            //plate.ObjectPlacement = toolkit_factory.MakeLinearPlacement(_model, diretrix, distance);
            return plate;
        }

        private IfcPlate CreateLGirderPlate(double distAlong,IfcCurve diretrix1,IfcCurve diretrix2,int Ltype=0,bool isTraverse=true)
        {

            var plate = this._model.Instances.New<IfcPlate>();
            plate.Name = "LGirder";
            plate.ObjectType = "PLATE";

            double TH = Math.Atan((0.75 * cross.girder_web_height*1000-170/2) / (Math.Abs(cross.girder_web_height / (cross.lateral_offset_dis[0]
            - cross.lateral_offset_dis[1]))*500));

            var solid = this._model.Instances.New<IfcExtrudedAreaSolid>();
            
            if (Ltype == 0)
            {
                if(isTraverse==true)
                {
                    var distance1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong, (cross.girder_web_thickness + cross.girder_upper_flange_width / 2) * 1000, -100);
                    var linearPlacement1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distance1);
                    var point1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement1);
                    var distance2 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong, (-cross.girder_web_thickness - cross.girder_upper_flange_width / 2) * 1000, -100);
                    var linearPlacement2 = toolkit_factory.MakeLinearPlacement(_model, diretrix2, distance2);
                    var point2 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement2);
                    var locPlacement = toolkit_factory.ToAixs3D_LateralGirder(_model, linearPlacement1);
                    solid.Position = locPlacement;
                    solid.SweptArea = toolkit_factory.MakeLGirderProfile(_model, 10, 10, 170, Ltype);
                    solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    solid.Depth = GetLength(point2, point1);
                }
                else
                {
                    var distance1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong, (cross.girder_web_thickness + cross.girder_upper_flange_width / 2) * 1000 + 40 +170
                        *Math.Sin(TH)/2, -100-170-40-170*Math.Cos(TH)/2);
                    var linearPlacement1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distance1);
                    var point1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement1);

                    var distancetmp1= toolkit_factory.MakeDistanceExpresstion(_model, distAlong, 0
                        , -100 - 0.75 * cross.girder_web_height * 1000);
                    var linearPlacementTmp1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distancetmp1);
                    var pointTmp1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacementTmp1);
                    var distancetmp2= toolkit_factory.MakeDistanceExpresstion(_model, distAlong, 0, 
                        -100 - 0.75 * cross.girder_web_height * 1000);
                    var linearPlacementTmp2 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix2, distancetmp2);
                    var pointTmp2 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacementTmp2);

                    var point2 = toolkit_factory.MakeCartesianPoint(_model, (pointTmp1.X + pointTmp2.X) / 2,
                        (pointTmp1.Y + pointTmp2.Y) / 2, ((pointTmp1.Z + pointTmp2.Z) / 2 + 170 + 170/2 + 40));
                    var locPlacement = toolkit_factory.MakeInclineToAxis3D(_model, linearPlacement1, point1, point2);
                    solid.Position = locPlacement;
                    solid.SweptArea = toolkit_factory.MakeLGirderProfile(_model, 10, 10, 170, Ltype);
                    solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    solid.Depth = GetLength(point2, point1)-170/Math.Tan(TH)/2-170/2/Math.Sin(TH)+30;
                }
            }
            if (Ltype == 1)
            {
                if(isTraverse==true)
                {
                    //这里的distance和板一不一样，为接板厚度
                    var distance1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong + 10, (cross.girder_web_thickness + cross.girder_upper_flange_width / 2) * 1000, -100);
                    var distance2 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong + 10, (-cross.girder_web_thickness - cross.girder_upper_flange_width / 2) * 1000, -100);
                    var linearPlacement1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distance1);
                    var point1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement1);
                    var linearPlacement2 = toolkit_factory.MakeLinearPlacement(_model, diretrix2, distance2);
                    var point2 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement2);
                    var locPlacement = toolkit_factory.ToAixs3D_LateralGirder(_model, linearPlacement1);
                    solid.Position = locPlacement;
                    solid.SweptArea = toolkit_factory.MakeLGirderProfile(_model, 10, 10, 170, Ltype);
                    solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    solid.Depth = GetLength(point2, point1);
                }
                else
                {
                    var distance1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong + 10, (cross.girder_web_thickness + cross.girder_upper_flange_width / 2) * 1000 + 40 + 170
                         * Math.Sin(TH) / 2, -100 - 170 - 40 - 170 * Math.Cos(TH) / 2);
                    var linearPlacement1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distance1);
                    var point1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement1);

                    var distancetmp1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong + 10, 0
                        , -100 - 0.75 * cross.girder_web_height * 1000);
                    var linearPlacementTmp1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distancetmp1);
                    var pointTmp1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacementTmp1);
                    var distancetmp2 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong + 10, 0,
                        -100 - 0.75 * cross.girder_web_height * 1000);
                    var linearPlacementTmp2 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix2, distancetmp2);
                    var pointTmp2 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacementTmp2);

                    var point2 = toolkit_factory.MakeCartesianPoint(_model, (pointTmp1.X + pointTmp2.X) / 2,
                        (pointTmp1.Y + pointTmp2.Y) / 2, ((pointTmp1.Z + pointTmp2.Z) / 2 + 170 + 170 / 2 + 40));
                    var locPlacement = toolkit_factory.MakeInclineToAxis3D(_model, linearPlacement1, point1, point2);
                    solid.Position = locPlacement;
                    solid.SweptArea = toolkit_factory.MakeLGirderProfile(_model, 10, 10, 170, Ltype);
                    solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    solid.Depth = GetLength(point2, point1) - 170 / Math.Tan(TH) / 2 - 170 / 2 / Math.Sin(TH) + 30;
                }

            }
            if (Ltype == 2)
            {
                if(isTraverse==true)
                {
                    var distance1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong, (cross.girder_web_thickness + cross.girder_upper_flange_width / 2) * 1000, -100 - 0.75 * cross.girder_web_height * 1000);
                    var distance2 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong, (-cross.girder_web_thickness - cross.girder_upper_flange_width / 2) * 1000, -100 - 0.75 * cross.girder_web_height * 1000);
                    var linearPlacement1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distance1);
                    var point1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement1);
                    var linearPlacement2 = toolkit_factory.MakeLinearPlacement(_model, diretrix2, distance2);
                    var point2 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement2);
                    var locPlacement = toolkit_factory.ToAixs3D_LateralGirder(_model, linearPlacement1);
                    solid.Position = locPlacement;
                    solid.SweptArea = toolkit_factory.MakeLGirderProfile(_model, 10, 10, 170, Ltype);
                    solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    solid.Depth = GetLength(point2, point1);
                }
                else
                {
                    var distance1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong, -(cross.girder_web_thickness + cross.girder_upper_flange_width / 2) * 1000 - 40 - 170
                          * Math.Sin(TH) / 2, -100 - 170 - 40 - 170 * Math.Cos(TH) / 2);
                    var linearPlacement1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix2, distance1);
                    var point1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement1);

                    var distancetmp1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong, 0
                        , -100 - 0.75 * cross.girder_web_height * 1000);
                    var linearPlacementTmp1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distancetmp1);
                    var pointTmp1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacementTmp1);
                    var distancetmp2 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong, 0,
                        -100 - 0.75 * cross.girder_web_height * 1000);
                    var linearPlacementTmp2 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix2, distancetmp2);
                    var pointTmp2 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacementTmp2);

                    var point2 = toolkit_factory.MakeCartesianPoint(_model, (pointTmp1.X + pointTmp2.X) / 2,
                        (pointTmp1.Y + pointTmp2.Y) / 2, ((pointTmp1.Z + pointTmp2.Z) / 2 + 170 + 170 / 2 + 40));
                    var locPlacement = toolkit_factory.MakeInclineToAxis3D(_model, linearPlacement1, point1, point2);
                    solid.Position = locPlacement;
                    solid.SweptArea = toolkit_factory.MakeLGirderProfile(_model, 10, 10, 170, Ltype);
                    solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    solid.Depth = GetLength(point2, point1) - 170 / Math.Tan(TH) / 2 - 170 / 2 / Math.Sin(TH) + 30;
                }
            }
            if (Ltype == 3)
            {
                if(isTraverse==true)
                {
                    var distance1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong + 10, (cross.girder_web_thickness + cross.girder_upper_flange_width / 2) * 1000, -100 - 0.75 * cross.girder_web_height * 1000);
                    var distance2 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong + 10, (-cross.girder_web_thickness - cross.girder_upper_flange_width / 2) * 1000, -100 - 0.75 * cross.girder_web_height * 1000);
                    var linearPlacement1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distance1);
                    var point1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement1);
                    var linearPlacement2 = toolkit_factory.MakeLinearPlacement(_model, diretrix2, distance2);
                    var point2 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement2);
                    var locPlacement = toolkit_factory.ToAixs3D_LateralGirder(_model, linearPlacement1);
                    solid.Position = locPlacement;
                    solid.SweptArea = toolkit_factory.MakeLGirderProfile(_model, 10, 10, 170, Ltype);
                    solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    solid.Depth = GetLength(point2, point1);
                }
                else
                {
                    var distance1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong + 10, -(cross.girder_web_thickness + cross.girder_upper_flange_width / 2) * 1000 - 40 - 170
                            * Math.Sin(TH) / 2, -100 - 170 - 40 - 170 * Math.Cos(TH) / 2);
                    var linearPlacement1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix2, distance1);
                    var point1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacement1);

                    var distancetmp1 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong + 10, 0
                        , -100 - 0.75 * cross.girder_web_height * 1000);
                    var linearPlacementTmp1 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix1, distancetmp1);
                    var pointTmp1 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacementTmp1);
                    var distancetmp2 = toolkit_factory.MakeDistanceExpresstion(_model, distAlong + 10, 0,
                        -100 - 0.75 * cross.girder_web_height * 1000);
                    var linearPlacementTmp2 = toolkit_factory.MakeLinearPlacement_LateralGirder(_model, diretrix2, distancetmp2);
                    var pointTmp2 = toolkit_factory.MakeLateralGirderPoint(_model, linearPlacementTmp2);

                    var point2 = toolkit_factory.MakeCartesianPoint(_model, (pointTmp1.X + pointTmp2.X) / 2,
                        (pointTmp1.Y + pointTmp2.Y) / 2, ((pointTmp1.Z + pointTmp2.Z) / 2 + 170 + 170 / 2 + 40));
                    var locPlacement = toolkit_factory.MakeInclineToAxis3D(_model, linearPlacement1, point1, point2);
                    solid.Position = locPlacement;
                    solid.SweptArea = toolkit_factory.MakeLGirderProfile(_model, 10, 10, 170, Ltype);
                    solid.ExtrudedDirection = toolkit_factory.MakeDirection(_model, 0, 0, 1);
                    solid.Depth = GetLength(point2, point1) - 170 / Math.Tan(TH) / 2 - 170 / 2 / Math.Sin(TH) + 30;
                }
            }
            toolkit_factory.SetSurfaceColor(_model, solid, 124.0 / 255.0, 51.0 / 255.0, 49.0 / 255.0, 0.15);
            var shape = toolkit_factory.MakeShapeRepresentation(_model, 3, "Body", "AdvancedSweptSolid", solid);

            plate.Representation = this._model.Instances.New<IfcProductDefinitionShape>(pd => pd.Representations.Add(shape));
            plate.PredefinedType = IfcPlateTypeEnum.USERDEFINED;
            return plate;
        }

        public double GetLength(IfcCartesianPoint p1,IfcCartesianPoint p2)
        {
            return Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y) + (p1.Z - p2.Z) * (p1.Z - p2.Z));
        }

        private IfcElementAssembly CreatePlatesets(IfcCurve diretrix)
        {
            using (var txn = this._model.BeginTransaction("CreatePlates"))
            {
                var plateAssembly = this._model.Instances.New<IfcElementAssembly>();
                plateAssembly.Name = "IGirder";
                plateAssembly.Description = "SteelGirder";
                plateAssembly.PredefinedType = IfcElementAssemblyTypeEnum.GIRDER;

                var relAggregates = this._model.Instances.New<IfcRelAggregates>();
                relAggregates.RelatingObject = plateAssembly;

                relAggregates.RelatedObjects.Add(CreatePlateset(diretrix));
                txn.Commit();
                return plateAssembly;
            }
        }

        public IfcElementAssembly CreateLateralConnection(IfcCurve diretrix1,IfcCurve diretrix2,double distAlong)
        {
            using (var txn = this._model.BeginTransaction("CreateLateralConnection"))
            {
                var plateAssembly = this._model.Instances.New<IfcElementAssembly>();
                plateAssembly.Name = "LateralConnection";
                plateAssembly.Description = "SteelLateralConnection";
                plateAssembly.PredefinedType = IfcElementAssemblyTypeEnum.USERDEFINED;

                var relAggregates = this._model.Instances.New<IfcRelAggregates>();
                relAggregates.RelatingObject = plateAssembly;

                for(int i=0;i<5;i++)
                {
                    relAggregates.RelatedObjects.Add(CreateConnectPlate(distAlong, diretrix1, diretrix2, i));
                }

                txn.Commit();
                return plateAssembly;
            }
        }

        public IfcElementAssembly CreateLateralGirder(IfcCurve diretrix1, IfcCurve diretrix2,double distAlong)
        {
            using (var txn = this._model.BeginTransaction("CreateLateralGirder"))
            {
                var plateAssembly = this._model.Instances.New<IfcElementAssembly>();
                plateAssembly.Name = "LateralGirder";
                plateAssembly.Description = "SteelLateralGirder";
                plateAssembly.PredefinedType = IfcElementAssemblyTypeEnum.USERDEFINED;

                var relAggregates = this._model.Instances.New<IfcRelAggregates>();
                relAggregates.RelatingObject = plateAssembly;

                for (int i = 0; i < 4; i++)
                {
                    relAggregates.RelatedObjects.Add(CreateLGirderPlate(distAlong, diretrix1, diretrix2, i));
                }
                for(int i=0;i<4;i++)
                {
                    relAggregates.RelatedObjects.Add(CreateLGirderPlate(distAlong, diretrix1, diretrix2, i, false));
                }
                txn.Commit();
                return plateAssembly;
            }
        }
        private void CreateMaterialForGirder(IfcElementAssembly girder)
        {
            using (var txn = this._model.BeginTransaction("Add material for girder"))
            {
                var material = _model.Instances.New<IfcMaterial>(mat =>
                {
                    mat.Name = "Q345";
                    mat.Category = "STEEL";
                });
                _model.Instances.New<IfcRelAssociatesMaterial>(ram =>
                {
                    ram.RelatingMaterial = material;
                    ram.RelatedObjects.Add(girder);
                });
                var pset_MaterialCommon = _model.Instances.New<IfcMaterialProperties>(mp =>
                {
                    mp.Name = "Pset_MaterialCommon";
                    mp.Material = material;
                    var massDensity = _model.Instances.New<IfcPropertySingleValue>(p =>
                    {
                        p.Name = "MassDensity";
                        p.NominalValue = new IfcMassDensityMeasure(7.85e-9);
                    });
                    mp.Properties.Add(massDensity);
                });
                var pset_MaterialMechanical = _model.Instances.New<IfcMaterialProperties>(mp =>
                {
                    mp.Name = "Pset_MaterialMechanical";
                    mp.Material = material;

                    var youngModulus = _model.Instances.New<IfcPropertySingleValue>(p =>
                    {
                        p.Name = "YoungModulus";
                        p.NominalValue = new IfcModulusOfElasticityMeasure(2.06e11);
                    });

                    var poissonRatio = _model.Instances.New<IfcPropertySingleValue>(p =>
                    {
                        p.Name = "PoissonRatio";
                        p.NominalValue = new IfcPositiveRatioMeasure(0.3);
                    });

                    var thermalExpansionCoefficient = _model.Instances.New<IfcPropertySingleValue>(p =>
                    {
                        p.Name = "ThermalExpansionCoefficient";
                        p.NominalValue = new IfcThermalExpansionCoefficientMeasure(1.2e-5);
                    });
                    mp.Properties.AddRange(new List<IfcPropertySingleValue>() { youngModulus, poissonRatio, thermalExpansionCoefficient });
                });
                txn.Commit();
            }
        }
        private void CreateMaterialForDeckPlate(IfcSlab deckPlate)
        {
            var material = _model.Instances.New<IfcMaterial>(mat =>
              {
                  mat.Name = "C50";
                  mat.Category = "CONC";
              });
            _model.Instances.New<IfcRelAssociatesMaterial>(ram =>
            {
                ram.RelatingMaterial = material;
                ram.RelatedObjects.Add(deckPlate);
            });
            var pset_MaterialCommon = _model.Instances.New<IfcMaterialProperties>(mp =>
              {
                  mp.Name = "Pset_MaterialCommon";
                  mp.Material = material;
                  var massDensity = _model.Instances.New<IfcPropertySingleValue>(p =>
                        {
                            p.Name = "MassDensity";
                            p.NominalValue = new IfcMassDensityMeasure(2.5e6);
                        });
                  mp.Properties.Add(massDensity);
              });
            var pset_MaterialMechanical = _model.Instances.New<IfcMaterialProperties>(mp =>
              {
                  mp.Name = "Pset_MaterialMechanical";
                  mp.Material = material;

                  var compressStrength = _model.Instances.New<IfcPropertySingleValue>(p =>
                    {
                        p.Name = "CompressStrength";
                        p.NominalValue = new IfcModulusOfElasticityMeasure(5e7);
                    });

                  var poissonRatio = _model.Instances.New<IfcPropertySingleValue>(p =>
                    {
                        p.Name = "PoissonRatio";
                        p.NominalValue = new IfcPositiveRatioMeasure(0.2);
                    });

                  var thermalExpansionCoefficient = _model.Instances.New<IfcPropertySingleValue>(p =>
                  {
                      p.Name = "ThermalExpansionCoefficient";
                      p.NominalValue = new IfcThermalExpansionCoefficientMeasure(1e-5);
                  });
                  mp.Properties.AddRange(new List<IfcPropertySingleValue>() { compressStrength, poissonRatio, thermalExpansionCoefficient });
              });
        }
    }
}
