using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.DataSourcesFile;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using System.Collections;

namespace LM.GIS
{
    public class FeatureLyrHelper
    {
        public string FeatureLayerName;
        public string FeatureClassName;
        public string FolderName;
        /// <summary>
        /// 点 线 面等
        /// Here is about the diference between the GeometryType and the Feature Type:http://m.blog.csdn.net/blog/u011170962/38562841
        /// </summary>
        public esriGeometryType GeometryType;

        public esriFeatureType FeatureType;

        public IFieldsEdit Fields;
        /// <summary>
        /// 空间参考
        /// </summary>
        public ISpatialReference SpatialReference;


        public bool ResolveJson(string jsonPath)
        {
            if (File.Exists(jsonPath))
            {
                string jsonStr= File.ReadAllText(jsonPath);
                JavaScriptSerializer js = new JavaScriptSerializer();
                js.MaxJsonLength = Int32.MaxValue;
                FeatureObjClass obj= js.Deserialize<FeatureObjClass>(jsonStr);

                //解析空间参考
                Dictionary<string, object> dict = (Dictionary<string,object>)obj.spatialReference;
                string wkt= dict["wkt"].ToString();
                string[] mySRArray= wkt.Split(new string[] { "PROJCS[", "," }, StringSplitOptions.None);
                string mySr = "esriSRProjCS_"+ mySRArray[1].Replace("\"", "").Replace("Xian_1980", "Xian1980");
                esriSRProjCS4Type srEnum = (esriSRProjCS4Type)Enum.Parse(typeof(esriSRProjCS4Type), mySr);
                //解析要素               

                //FeatureClass Name
                setNameAndGeotype(obj.displayFieldName, obj.geometryType, (int)srEnum, obj.fields,obj.features);
            }
            
            return true;
        }

        public bool setNameAndGeotype(string className, esriGeometryType geoType, int sr,List<myFieldEdit> jsonFields,List<myFeatures> jsonFeatures)
        {
            this.FeatureClassName = className;
            this.GeometryType = geoType; //(esriGeometryType)Enum.Parse(typeof(esriGeometryType),geoType);


            ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass();
            ISpatialReference spatialReference =
                spatialReferenceFactory.CreateProjectedCoordinateSystem(sr);

            //添加字段
            IGeometryDefEdit pGeomDef = new GeometryDefClass();
            pGeomDef.GeometryType_2 = this.GeometryType;
            pGeomDef.SpatialReference_2 = spatialReference;
            //new UnknownCoordinateSystemClass();// 
            this.Fields = new FieldsClass();

            IFieldEdit pField = new FieldClass();        
            pField.Type_2 = esriFieldType.esriFieldTypeGeometry;
            pField.GeometryDef_2 = pGeomDef;
            pField.Name_2 = "Shape ";
            this.Fields.AddField(pField);
            

            for (int i = 0; i < jsonFields.Count; i++)
            {
                myFieldEdit myField = jsonFields[i];
                AddNewFields(myField, this.Fields);
            }

            string folderName = GetFloderName();
            IFeatureWorkspace pFeatureWorkspace = CreateFeatureWorkSpace(folderName);
          

            IFeatureClass pFeatureClass = CreateFeatureClass(pFeatureWorkspace, this.FeatureClassName, spatialReference,
                esriFeatureType.esriFTSimple,
                this.GeometryType, this.Fields, null, null, "");

            AddFeatures(pFeatureClass, jsonFeatures,jsonFields);

            MessageBox.Show("添加成功");                   
            return true;
        }        

        public bool AddNewFields(myFieldEdit myField, IFieldsEdit pFields)
        {
            try
            {
                IFieldEdit pField = new FieldClass();

                if (myField.name.ToLower() == "objectid")
                {
                    pField.Type_2 = esriFieldType.esriFieldTypeInteger;                   
                }
                else
                {
                    pField.Type_2 = (esriFieldType)Enum.Parse(typeof(esriFieldType), myField.type);
                }                

                //esriFieldType.esriFieldTypeInteger;

                pField.Name_2 = myField.name;
                if (myField.length != 0)
                {
                    pField.Length_2 = myField.length;
                }                                        
                pFields.AddField(pField);
            }
            catch (Exception ex)
            {
                throw;
            }
            return true;
        }

        /// 创建要素类
        /// summary;
        /// param name="pObject";IWorkspace或者IFeatureDataset对象;
        /// param name="pName";要素类名称;
        /// param name="pSpatialReference";空间参考;
        /// param name="pFeatureType";要素类型;
        /// param name="pGeometryType";几何类型;
        /// param name="pFields";字段集;
        /// param name="pUidClsId";CLSID值;
        /// param name="pUidClsExt";EXTCLSID值;
        /// param name="pConfigWord";配置信息关键词;
        /// 返回IFeatureClass
        public IFeatureClass CreateFeatureClass(object pObject, string pName, ISpatialReference pSpatialReference,
            esriFeatureType pFeatureType,
            esriGeometryType pGeometryType, IFields pFields, UID pUidClsId, UID pUidClsExt, string pConfigWord)
        {
            #region 错误检测

            if (pObject == null)
            {
                throw new Exception("[pObject] 不能为空!");
            }
            if (!((pObject is IFeatureWorkspace) || (pObject is IFeatureDataset)))
            {
                throw (new Exception("[pObject] 必须为IFeatureWorkspace 或者 IFeatureDataset"));
            }
            if (pName.Length == 0)
            {
                throw (new Exception("[pName] 不能为空!"));
            }
            if ((pObject is IWorkspace) && (pSpatialReference == null))
            {
                throw (new Exception("[pSpatialReference] 不能为空(对于单独的要素类)"));
            }

            #endregion

            #region pUidClsID字段为空时

            if (pUidClsId == null)
            {
                pUidClsId = new UIDClass();
                switch (pFeatureType)
                {
                    case (esriFeatureType.esriFTSimple):
                        if (pGeometryType == esriGeometryType.esriGeometryLine)
                            pGeometryType = esriGeometryType.esriGeometryPolyline;
                        pUidClsId.Value = "{52353152-891A-11D0-BEC6-00805F7C4268}";
                        break;
                    case (esriFeatureType.esriFTSimpleJunction):
                        pGeometryType = esriGeometryType.esriGeometryPoint;
                        pUidClsId.Value = "{CEE8D6B8-55FE-11D1-AE55-0000F80372B4}";
                        break;
                    case (esriFeatureType.esriFTComplexJunction):
                        pUidClsId.Value = "{DF9D71F4-DA32-11D1-AEBA-0000F80372B4}";
                        break;
                    case (esriFeatureType.esriFTSimpleEdge):
                        pGeometryType = esriGeometryType.esriGeometryPolyline;
                        pUidClsId.Value = "{E7031C90-55FE-11D1-AE55-0000F80372B4}";
                        break;
                    case (esriFeatureType.esriFTComplexEdge):
                        pGeometryType = esriGeometryType.esriGeometryPolyline;
                        pUidClsId.Value = "{A30E8A2A-C50B-11D1-AEA9-0000F80372B4}";
                        break;
                    case (esriFeatureType.esriFTAnnotation):
                        pGeometryType = esriGeometryType.esriGeometryPolygon;
                        pUidClsId.Value = "{E3676993-C682-11D2-8A2A-006097AFF44E}";
                        break;
                    case (esriFeatureType.esriFTDimension):
                        pGeometryType = esriGeometryType.esriGeometryPolygon;
                        pUidClsId.Value = "{496764FC-E0C9-11D3-80CE-00C04F601565}";
                        break;
                }
            }

            #endregion

            #region pUidClsExt字段为空时

            if (pUidClsExt == null)
            {
                switch (pFeatureType)
                {
                    case esriFeatureType.esriFTAnnotation:
                        pUidClsExt = new UIDClass();
                        pUidClsExt.Value = "{24429589-D711-11D2-9F41-00C04F6BC6A5}";
                        break;
                    case esriFeatureType.esriFTDimension:
                        pUidClsExt = new UIDClass();
                        pUidClsExt.Value = "{48F935E2-DA66-11D3-80CE-00C04F601565}";
                        break;
                }
            }

            #endregion

            #region 字段集合为空时

            if (pFields == null)
            {
                //实倒化字段集合对象
                pFields = new FieldsClass();
                IFieldsEdit tFieldsEdit = (IFieldsEdit)pFields;

                //创建几何对象字段定义
                IGeometryDef tGeometryDef = new GeometryDefClass();
                IGeometryDefEdit tGeometryDefEdit = tGeometryDef as IGeometryDefEdit;

                //指定几何对象字段属性值
                tGeometryDefEdit.GeometryType_2 = pGeometryType;
                tGeometryDefEdit.GridCount_2 = 1;
                tGeometryDefEdit.set_GridSize(0, 1000);
                if (pObject is IWorkspace)
                {
                    tGeometryDefEdit.SpatialReference_2 = pSpatialReference;
                }

                //创建OID字段
                IField fieldOID = new FieldClass();
                IFieldEdit fieldEditOID = fieldOID as IFieldEdit;
                fieldEditOID.Name_2 = "OBJECTID";
                fieldEditOID.AliasName_2 = "OBJECTID";
                fieldEditOID.Type_2 = esriFieldType.esriFieldTypeOID;
                tFieldsEdit.AddField(fieldOID);

                //创建几何字段
                IField fieldShape = new FieldClass();
                IFieldEdit fieldEditShape = fieldShape as IFieldEdit;
                fieldEditShape.Name_2 = "SHAPE";
                fieldEditShape.AliasName_2 = "SHAPE";
                fieldEditShape.Type_2 = esriFieldType.esriFieldTypeGeometry;
                fieldEditShape.GeometryDef_2 = tGeometryDef;
                tFieldsEdit.AddField(fieldShape);
            }

            #endregion

            //几何对象字段名称
            string strShapeFieldName = "";
            for (int i = 0; i < pFields.FieldCount; i++)
            {
                if (pFields.get_Field(i).Type == esriFieldType.esriFieldTypeGeometry)
                {
                    strShapeFieldName = pFields.get_Field(i).Name;
                    break;
                }
            }

            if (strShapeFieldName.Length == 0)
            {              
                throw (new Exception("字段集中找不到几何对象定义"));
            }

            IFeatureClass tFeatureClass = null;
            if (pObject is IWorkspace)
            {
                //创建独立的FeatureClass
                IWorkspace tWorkspace = pObject as IWorkspace;
                IFeatureWorkspace tFeatureWorkspace = tWorkspace as IFeatureWorkspace;                
                try
                {
                    tFeatureClass = tFeatureWorkspace.CreateFeatureClass(pName, pFields, pUidClsId, pUidClsExt, pFeatureType,
                   strShapeFieldName, pConfigWord);
                }
                catch (Exception ex) {

                }
                
                            
                                                     
            }
            else if (pObject is IFeatureDataset)
            {
                //在要素集中创建FeatureClass
                IFeatureDataset tFeatureDataset = (IFeatureDataset)pObject;
                tFeatureClass = tFeatureDataset.CreateFeatureClass(pName, pFields, pUidClsId, pUidClsExt, pFeatureType,
                    strShapeFieldName, pConfigWord);
            }

            return tFeatureClass;
        }


        private IFeatureWorkspace CreateFeatureWorkSpace(string fileName)
        {
            try
            {
                IWorkspaceFactory ipWorkspaceFactory;
                IWorkspace ipWorkspace;
                IFeatureWorkspace ipFeatureWorkspace;
                ipWorkspaceFactory = new ShapefileWorkspaceFactoryClass();
                //ipWorkspace = ipWorkspaceFactory.Create(directName,fileName,null,0)
                ipWorkspace = ipWorkspaceFactory.OpenFromFile(fileName, 0);
                ipFeatureWorkspace = ipWorkspace as IFeatureWorkspace;
                return ipFeatureWorkspace;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        public string GetFloderName()
        {
            FolderBrowserDialog fbdg = new FolderBrowserDialog();
            fbdg.ShowNewFolderButton = true;
            if (fbdg.ShowDialog() == DialogResult.OK)
            {
                return fbdg.SelectedPath;
            }
            else
            {
                return string.Empty;
            }
        }

        public void ExportFeature(IFeatureClass pInFeatureClass, string pPath)
        {
            // create a new Access workspace factory       
            IWorkspaceFactory pWorkspaceFactory = new ShapefileWorkspaceFactoryClass();
            string parentPath = pPath.Substring(0, pPath.LastIndexOf('\\'));
            string fileName = pPath.Substring(pPath.LastIndexOf('\\') + 1, pPath.Length - pPath.LastIndexOf('\\') - 1);
            IWorkspaceName pWorkspaceName = pWorkspaceFactory.Create(parentPath, fileName, null, 0);
            // Cast for IName       
            IName name = (IName)pWorkspaceName;
            //Open a reference to the access workspace through the name object       
            IWorkspace pOutWorkspace = (IWorkspace)name.Open();

            IDataset pInDataset = pInFeatureClass as IDataset;
            IFeatureClassName pInFCName = pInDataset.FullName as IFeatureClassName;
            IWorkspace pInWorkspace = pInDataset.Workspace;
            IDataset pOutDataset = pOutWorkspace as IDataset;
            IWorkspaceName pOutWorkspaceName = pOutDataset.FullName as IWorkspaceName;
            IFeatureClassName pOutFCName = new FeatureClassNameClass();
            IDatasetName pDatasetName = pOutFCName as IDatasetName;
            pDatasetName.WorkspaceName = pOutWorkspaceName;
            pDatasetName.Name = pInFeatureClass.AliasName;
            IFieldChecker pFieldChecker = new FieldCheckerClass();
            pFieldChecker.InputWorkspace = pInWorkspace;
            pFieldChecker.ValidateWorkspace = pOutWorkspace;
            IFields pFields = pInFeatureClass.Fields;
            IFields pOutFields;
            IEnumFieldError pEnumFieldError;
            pFieldChecker.Validate(pFields, out pEnumFieldError, out pOutFields);
            IFeatureDataConverter pFeatureDataConverter = new FeatureDataConverterClass();
            pFeatureDataConverter.ConvertFeatureClass(pInFCName, null, null, pOutFCName, null, pOutFields, "", 100, 0);
        }

        public void AddAttrsForFeatures(IFeatureClass featClass, List<myFeatures> jsonFeatures, List<myFieldEdit> fieldList)
        {
            //IQueryFilter pQueryFilter = new QueryFilterClass();
            //pQueryFilter.WhereClause = "";
            //IFeatureCursor pFeatureCursor = featClass.Search(pQueryFilter, false);
            //IFeature pFeature = pFeatureCursor.NextFeature();
            //while (pFeature != null)
            //{
            //    pFeature = pFeatureCursor.NextFeature();
            //}
        }

        public void AddFeatures(IFeatureClass featClass,List<myFeatures> jsonFeatures,List<myFieldEdit> fieldList)
        {
            for (int i = 0; i < jsonFeatures.Count; i++)
            {
                myFeatures _feature = jsonFeatures[i];
                Dictionary<string, object> geoDict = (Dictionary<string, object>)_feature.geometry;
                //属性值
                Dictionary<string, object> attrDict = (Dictionary<string, object>)_feature.attributes;
                //IFeature pFeature = featClass.CreateFeature();
                IFeatureBuffer pFeature = featClass.CreateFeatureBuffer();
                IFeatureCursor pFeatureCursor = featClass.Insert(true);


                //解析几何
                if (featClass.ShapeType == esriGeometryType.esriGeometryPoint)
                {

                }
                else if (featClass.ShapeType == esriGeometryType.esriGeometryPolyline)
                {
                    //pc = new PolylineClass();
                }
                else if (featClass.ShapeType == esriGeometryType.esriGeometryPolygon)
                {
                    //pc = new PolygonClass();
                    object ringsObj = geoDict["rings"];
                    IPointCollection pc = new PolygonClass();
                    object[] ringList1 = ringsObj as object[];
                    for (int j = 0; j < ringList1.Length; j++)
                    {
                        object[] ringList2 = ringList1.GetValue(j) as object[];

                        //System.Threading.Tasks.Parallel.For(0, ringList2.Length, (item, pls) => {
                        //    object[] ringList3 = ringList2.GetValue(item) as object[];
                        //    IPoint point = new PointClass();
                        //    point.X = Convert.ToDouble(ringList3[0]);
                        //    point.Y = Convert.ToDouble(ringList3[1]);
                        //    pc.AddPoint(point);
                        //});

                        for (int k = 0; k < ringList2.Length; k++)
                        {
                            object[] ringList3 = ringList2.GetValue(k) as object[];
                            IPoint point = new PointClass();
                            point.X = Convert.ToDouble(ringList3[0]);
                            point.Y = Convert.ToDouble(ringList3[1]);
                            pc.AddPoint(point);
                        }
                    }
                    pFeature.Shape = pc as IGeometry;
                }
                //pFeature.Store();
                for (int m = 0; m < fieldList.Count; m++)
                {
                    string fieldName = fieldList[m].name;
                    if (fieldList[m].type == "esriFieldTypeOID")
                        continue;                   
                    int fieldIndex = pFeature.Fields.FindField(fieldName);
                    pFeature.set_Value(fieldIndex, attrDict[fieldName]);                                     
                }
                //pFeature.Store();
                pFeatureCursor.InsertFeature(pFeature);
            }
        }

    }

    public class FeatureObjClass
    {
        public string displayFieldName;
        public List<object> fieldAliases;
        public esriGeometryType geometryType;
        public object spatialReference;
        public List<myFieldEdit> fields;
        public List<myFeatures> features;
    }
    public class myFieldEdit
    {
        public string name;
        public string type;
        public string alias;
        public int length;
    }

    public class myFeatures
    {
        public object attributes;
        public object geometry;
    }   
}
