//==================================================
// 
//  Copyright 2017 Siemens Product Lifecycle Management Software Inc. All Rights Reserved.
//
//==================================================



using System;

using Teamcenter.ClientX;
using Teamcenter.Schemas.Soa._2006_03.Exceptions;

//Include the Saved Query Service Interface
using Teamcenter.Services.Strong.Query;

// Input and output structures for the service operations 
// Note: the different namespace from the service interface
using Teamcenter.Services.Strong.Query._2006_03.SavedQuery;

using Teamcenter.Services.Strong.Core;
using Teamcenter.Soa.Client.Model;

using ImanQuery = Teamcenter.Soa.Client.Model.Strong.ImanQuery;

using System.Collections;


// Input and output structures for the service operations
// Note: the different namespace from the service interface
using Teamcenter.Services.Strong.Core._2006_03.DataManagement;
using Teamcenter.Services.Strong.Core._2008_06.DataManagement;

using ItemRevision = Teamcenter.Soa.Client.Model.Strong.ItemRevision;
using Teamcenter.Services.Strong.Query._2007_06.SavedQuery;
using Teamcenter.Services.Strong.Workflow;
using Teamcenter.Services.Strong.Workflow._2008_06.Workflow;
using cfg = soa.Entity.TCConfiguration;
using Teamcenter.Soa.Client;
using Teamcenter.Services.Loose.Core._2006_03.FileManagement;
using System.IO;
using Newtonsoft.Json.Linq;

namespace soa.Controllers
{
    public class DataManagement
    {

        /// <summary>
        ///     未完成：1.额外属性 自制外购 类型，还未对应实体环境的扩展字段，暂用user_data_2替代。
        ///     未完成：2.新增查询构建器，查询当前ITEM最新的版本
        ///     完成：3.根据ModelObject获取相应的属性
        ///     完成：4.在TC新增发布流程
        /// </summary>
        /// <param name="itemType">创建TC中ITEM的类型</param>
        /// <param name="codeNumber">物料号</param>
        /// <param name="CodeName">物料名称</param>
        /// <param name="longDetail">详细描述</param>
        /// <param name="unit">单位</param>
        /// <param name="productionType">物料属性（自制、外购、委外）</param>
        /// <param name="ReqName">物料申请人</param>
        public String createTCItem(String codeNumber, String CodeName, String longDetail, String unit, String productionType, String ReqName)
        {
            String erroMsg = "";
            //处理详细描述,10-79插入详细描述，80-90不插入详细描述
            longDetail = codeNumber.Length < 2 ? ""
                        : (codeNumber.Substring(0, 2).CompareTo("80") >= 0 ? "" : longDetail);

            String itemType = codeNumber.Length >= 2 && codeNumber.Substring(0, 2).Equals("80") ?
                cfg.get("CPTyep") : cfg.get("LBJType");

            try
            {
                

                DataManagementService dmService = DataManagementService.getService(Session.getConnection());

                //查询最新的ITEM版本
                //未完成2
                //ModelObject LastestRevision = findModel("MY_WEB_ITEM_REVISION", new string[] { "iid" }, new string[] { codeNumber });
                ModelObject LastestRevision = findModel(cfg.get("query_builder_lastestRevisionById_name")
                    , new string[] { cfg.get("query_builder_lastestRevisionById_queryKey") }, new string[] { codeNumber });
                if ( null!= LastestRevision && !string.IsNullOrEmpty(LastestRevision.Uid))
                {      
                    dmService.GetProperties(new ModelObject[] { LastestRevision  }, new string[] { "release_status_list" });
                    dmService.GetProperties(new ModelObject[] { LastestRevision }, new string[] { "item_revision_id" });
                    dmService.GetProperties(new ModelObject[] { LastestRevision }, new string[] { "object_name" }); 
                    dmService.GetProperties(new ModelObject[] { LastestRevision }, new string[] { "IMAN_master_form_rev" });
                    var master = LastestRevision.GetProperty("IMAN_master_form_rev").ModelObjectArrayValue[0];
                    dmService.GetProperties(new ModelObject[] { master }, new string[] { cfg.get("exAttr_detail") });
                    dmService.GetProperties(new ModelObject[] { LastestRevision }, new string[] { "IMAN_specification" });                    
                    String item_revision_id = LastestRevision.GetProperty("item_revision_id").StringValue.ToString();
                    
                    //如果名称、规格相同，不执行更新。
                    if (LastestRevision.GetProperty("object_name").StringValue.Equals(CodeName)
                        && master.GetProperty(cfg.get("exAttr_detail")).StringValue.Equals(longDetail))
                        return erroMsg;

                    if (LastestRevision.GetProperty("IMAN_specification").ModelObjectArrayValue.Length > 0)
                    {
                        return codeNumber + "/" + item_revision_id + "：有图纸，请在TC中更新。";
                    }

                    ModelObject release_status_obj = null;
                    if (LastestRevision.GetProperty("release_status_list").ModelObjectArrayValue.Length>0)
                    {
                        release_status_obj = LastestRevision.GetProperty("release_status_list").ModelObjectArrayValue[0];
                        dmService.GetProperties(new ModelObject[] { release_status_obj }, new string[] { "name" });
                    }
                    String release_status = null == release_status_obj ? "" : release_status_obj.GetProperty("name").StringValue;

                    //查询是否存在未发布版本
                    if (!release_status.Equals(cfg.get("publish_status_value")))
                        workflow_publish(cfg.get("publish_workflow"), LastestRevision);

                    //创建新版本前，修改ITEM数据
                    updateItem(codeNumber, CodeName, longDetail);
                    //创建新版本
                    reviseItem(LastestRevision, CodeName, longDetail, productionType, item_revision_id);
                }
                else
                {

                    //开始新增ITEM
                    //根据物料号创建ITEMID
                    ItemProperties itemProperty = new ItemProperties();
                    itemProperty.Type = itemType;   //创建ITEM的类型
                    itemProperty.ItemId = codeNumber;   //物料代码
                    itemProperty.Name = CodeName;    //物料名称
                    itemProperty.RevId = "00";    //版本
                    itemProperty.Description = "";    //描述
                    itemProperty.Uom = unit;  //单位

                    //增加额外属性
                    ExtendedAttributes exAttr = new ExtendedAttributes();
                    exAttr.Attributes = new Hashtable();
                    exAttr.ObjectType = itemType + "RevisionMaster";      //对应哪个form表
                    exAttr.Attributes[cfg.get("exAttr_productionType")] = productionType;  //需要替换

                    //增加额外属性
                    ExtendedAttributes exAttr2 = new ExtendedAttributes();
                    exAttr2.Attributes = new Hashtable();
                    exAttr2.ObjectType = itemType + "RevisionMaster";      //对应哪个form表
                    exAttr2.Attributes[cfg.get("exAttr_detail")] = longDetail;  //需要替换

                    itemProperty.ExtendedAttributes = new ExtendedAttributes[] { exAttr, exAttr2 };

                    //链接服务器创建Item
                    CreateItemsResponse response = dmService.CreateItems(new ItemProperties[] { itemProperty }, null, "");

                    if (response.ServiceData.sizeOfPartialErrors() > 0)
                    {
                        return "创建ITEM失败。" + response.ServiceData.GetPartialError(0).Messages[0];
                    }
                    //结束新增ITEM

                    //新增完后附加文件。
                    ModelObject itemReversion2add = findModel(cfg.get("query_builder_lastestRevisionById_name")
                    , new string[] { cfg.get("query_builder_lastestRevisionById_queryKey") }, new string[] { codeNumber });

                    //ModelObject datasets = createEmptyFile("Text",codeNumber, "./template/url.txt", "Text");
                    //createRelations(itemReversion2add, datasets, "IMAN_specification");
                    //changeOnwer(ReqName, datasets);

                    //创建dataset并关联，修改所有者
                    
                    if(codeNumber.Length > 4 &&
                        //codeNumber.Substring(0,2).CompareTo("80") >= 0)
                        codeNumber.Substring(0, 2).Equals("80"))
                    {
                        var subCodeNumber = codeNumber.Substring(0, 4);
                        var uploadCfgPramary = subCodeNumber.Equals("8102") || subCodeNumber.Equals("8301") ?
                            "uploadFile_part" : "uploadFile_asm";
                        var cfgList = cfg.tc[uploadCfgPramary].ToObject<ArrayList>();
                        foreach (var uploadObj in cfgList)
                        {
                            var uploadCfg = JObject.Parse(uploadObj.ToString());
                            ModelObject datasets_temp = createEmptyFile(uploadCfg["datasetType"].ToString()
                                , uploadCfg["fileType"].ToString() + "/" + codeNumber
                                , uploadCfg["filePath"].ToString()
                                , uploadCfg["fileRefName"].ToString());
                            createRelations(itemReversion2add, datasets_temp, uploadCfg["relationType"].ToString());
                            changeOnwer(ReqName, datasets_temp);
                        }
                    }
                }

                //调用查询构建器，查询ITEM和ITEMRevision
                ModelObject itemObj = findModel(cfg.get("query_builder_ItemById_name")
                    , new string[] { cfg.get("query_builder_ItemById_queryKey") }, new string[] { codeNumber });
                ModelObject itemReversion = findModel(cfg.get("query_builder_lastestRevisionById_name")
                    , new string[] { cfg.get("query_builder_lastestRevisionById_queryKey") }, new string[] { codeNumber });               
                if (null == itemObj || null == itemReversion)
                {
                    return "查询构建器失败。";
                }

                //修改所有者
                changeOnwer(ReqName, itemObj);
                changeOnwer(ReqName, itemReversion);

                //发布-外购件
                if( codeNumber.Length>=2 && (codeNumber.Substring(0, 2).CompareTo("80") < 0))
                    workflow_publish(cfg.get("publish_workflow"), itemReversion);
            }
            catch(Exception e)
            {
                deleteItem(codeNumber);
                throw e;
            }

            return erroMsg;
        }

        public string demo()
        {
            string res = string.Empty;
            DataManagementService dmService = DataManagementService.getService(Session2.getConnection());
            try
            {
                //未完成2
                ModelObject itemReversion = findModel(cfg.get("query_builder_lastestRevisionById_name")
                    , new string[] { cfg.get("query_builder_lastestRevisionById_queryKey") }, new string[] { "JLD100158" });
                var data = dmService.GetProperties(new ModelObject[] { itemReversion }, new string[] { "WL_REV_013" });
                res = itemReversion.GetProperty("").ModelObjectValue.Uid;
            }
            catch(Exception e)
            {
                res = "";
            }
            return res;
        }

        public void updateItem(String codeNumber, String name, String longDetail)
        {
            try
            {
                DataManagementService dmService = DataManagementService.getService(Session2.getConnection());
                ModelObject itemObj = findModel(cfg.get("query_builder_ItemById_name")
                    , new string[] { cfg.get("query_builder_ItemById_queryKey") }, new string[] { codeNumber });
                var item = new ItemElementProperties();
                item.ItemElement = itemObj;
                item.Name = name;
                //Hashtable kv = new Hashtable();
                //kv.Add("object_desc", longDetail);
                //item.ItemElemAttributes = kv;
                CreateOrUpdateItemElementsResponse rsp = dmService.CreateOrUpdateItemElements(new ItemElementProperties[] { item });

            }
            catch (Exception)
            {

            }
        }


        public void workflow_publish(String wfTemplate,ModelObject obj)
        {
            WorkflowService wfService = WorkflowService.getService(Session2.getConnection());

            

            if (wfService == null)
            {
                throw new Exception("The workflow service not found in Teamcenter.");
            }

            String[] arrObjectUID = new String[] { obj.Uid };
            int[] arrTypes = new int[arrObjectUID.Length];
            arrTypes[0] = 1;

            ContextData contextData = new ContextData();
            contextData.AttachmentCount = arrObjectUID.Length;//
            contextData.Attachments = arrObjectUID;//List of UID of objects to submit to workflow
            contextData.AttachmentTypes = arrTypes; //Types of attachment  EPM_target_attachment (target attachment) and EPM_reference_attachment (reference attachment).
            contextData.ProcessTemplate = wfTemplate;//"ReleaseObjectsWorkflow";
            
            InstanceInfo instanceResponse = wfService.CreateInstance(true, null, "maxtt-syn-process", null, "", contextData);

            if (instanceResponse.ServiceData.sizeOfPartialErrors() == 0)
            {
                //System.out.println("Process created Successfully");
            }
            else
            {
                throw new Exception("Submit To Workflow: 001"+ "Submit To Workflow - " + instanceResponse.ServiceData.GetPartialError(0).Messages[0]);
            }
        }


        public void reviseItem(ModelObject obj, String Name, String longDetail, String productionType, String item_revision_id) //throws ServiceException
        {
            String newVersionNumber = (int.Parse(item_revision_id) + 1 ).ToString().PadLeft(2, '0');

            DataManagementService dmService = DataManagementService.getService(Session2.getConnection());

            


            ReviseInfo rev = new ReviseInfo();
            rev.BaseItemRevision = new ItemRevision(null, obj.Uid);
            rev.ClientId = Name + "/" + newVersionNumber;
            rev.Description = "";
            rev.Name = Name;
            rev.NewRevId = newVersionNumber;

            //额外的表单属性
            PropertyNameValueInfo info = new PropertyNameValueInfo();
            info.PropertyName = cfg.get("exAttr_productionType");
            info.PropertyValues = new string[] { productionType };

            PropertyNameValueInfo info2 = new PropertyNameValueInfo();
            info2.PropertyName = cfg.get("exAttr_detail");
            info2.PropertyValues = new string[] { longDetail };

            rev.NewItemRevisionMasterProperties.PropertyValueInfo = new PropertyNameValueInfo[] { info, info2 };

            // *****************************
            ReviseResponse2 revised = dmService.Revise2(new ReviseInfo[] { rev });

            if (revised.ServiceData.sizeOfPartialErrors() > 0)
                throw new ServiceException("DataManagementService.revise returned a partial error." + revised.ServiceData.GetPartialError(0).Messages[0]);
        }

        public void deleteItems_single(ModelObject items) //throws ServiceException
        {
            // Get the service stub
            DataManagementService dmService = DataManagementService.getService(Session.getConnection());

            // *****************************
            // Execute the service operation
            // *****************************
            ServiceData serviceData = dmService.DeleteObjects(new ModelObject[] { items });


            // The AppXPartialErrorListener is logging the partial errors returned
            // In this simple example if any partial errors occur we will throw a
            // ServiceException
            if (serviceData.sizeOfPartialErrors() > 0)
                throw new ServiceException("DataManagementService.deleteObjects returned a partial error.");
        }

        public String deleteItem(String codeNumber)
        {
            String Msg = "执行成功";
            DataManagementService dmService = DataManagementService.getService(Session2.getConnection());

            ////删除前，取消发布
            //ModelObject itemReversion = findModel("", new string[] { "iid" }, new string[] { codeNumber });
            ////取消发布流程
            //workflow_publish("", itemReversion);
            
            //调用查询构建器，查询ITEM
            ModelObject itemObj = findModel(cfg.get("query_builder_ItemById_name")
                    , new string[] { cfg.get("query_builder_ItemById_queryKey") }, new string[] { codeNumber });
            ServiceData serviceData = dmService.DeleteObjects(new ModelObject[] { itemObj });

            //if (serviceData.sizeOfPartialErrors() > 0)
            //{
            //    Msg = "删除ITEM失败,已发布的ITEM不能删除或无权限删除:" + serviceData.GetPartialError(0).Messages[0];
            //    throw new Exception("删除ITEM失败,已发布的ITEM不能删除或无权限删除:" + serviceData.GetPartialError(0).Messages[0]);       
            //}
                

            return Msg;
        }



        //重构查询器
        /// <summary>
        /// 
        /// </summary>
        /// <param name="queryName">查询构建器的主键名称</param>
        /// <param name="keys">查询条件key</param>
        /// <param name="values">查询 条件values</param>
        /// <returns></returns>
        public ModelObject findModel(String queryName,String[] keys,string[] values)
        {
            ImanQuery query = null;
            // Get the service stub
            SavedQueryService queryService = SavedQueryService.getService(Session.getConnection());

            try
            {

                // *****************************
                // Execute the service operation
                // *****************************
                GetSavedQueriesResponse savedQueries = queryService.GetSavedQueries();


                if (savedQueries.Queries.Length == 0)
                {
                    throw new Exception("TC不存在查询构建器");
                }

                // Find one called 'Item Name'
                for (int i = 0; i < savedQueries.Queries.Length; i++)
                {
                    
                    if (savedQueries.Queries[i].Name.Equals(queryName))
                    {
                        query = savedQueries.Queries[i].Query;
                        break;
                    }
                }
            }
            catch (ServiceException e)
            {
                throw e;
            }

            if (query == null)
            {
                throw new Exception("不存在查询构建器:"+ queryName);
            }

            try
            {
                SavedQueryInput[] savedQueryInput = new SavedQueryInput[1];
                savedQueryInput[0] = new SavedQueryInput();
                savedQueryInput[0].Query = query;
                savedQueryInput[0].MaxNumToReturn = 25;
                savedQueryInput[0].LimitListCount = 0;
                savedQueryInput[0].LimitList = new Teamcenter.Soa.Client.Model.ModelObject[0];
                savedQueryInput[0].Entries = keys;//Attribute in Query to search by
                savedQueryInput[0].Values = values;
                savedQueryInput[0].MaxNumToReturn = 25;



                ExecuteSavedQueriesResponse savedQueryResult = queryService.ExecuteSavedQueries(savedQueryInput);
                SavedQueryResults found = savedQueryResult.ArrayOfResults.Length > 0 ? 
                    savedQueryResult.ArrayOfResults[0] : new SavedQueryResults();

                ModelObject[] modelObjs = found.Objects;

                return found.Objects.Length>0 ? found.Objects[0] : null;
            }
            catch (Exception e)
            {
                throw e;
            }
        }


        public void changeOnwer(String userName, ModelObject modl)
        {
            DataManagementService dmService = DataManagementService.getService(Session.getConnection());


            //ModelObject user = findUser(userName);
            ModelObject user = findModel(cfg.get("query_builder_userByUname_name")
                , new string[] { cfg.get("query_builder_userByUname_queryKey") }, new string[] { userName });
            if (null == user)
            {
                throw new Exception("构建器查找用户失败，请确认申请人在TC是否存在。");
            }

            //根据USER查找GROUP
            dmService.GetProperties(new ModelObject[] { user }, new string[] { "default_group" });
            ModelObject userGroup = user.GetProperty("default_group").ModelObjectValue;

            if (null == userGroup)
            {
                throw new Exception("构建器查找用户组失败。");
            }

            
            ObjectOwner[] ownerData = new ObjectOwner[1];

            ObjectOwner ownrObj = new ObjectOwner();
            ownrObj.Object = modl;
            ownrObj.Group = (Teamcenter.Soa.Client.Model.Strong.Group)userGroup;
            ownrObj.Owner = (Teamcenter.Soa.Client.Model.Strong.User)user;
            ownerData[0] = ownrObj;


            ServiceData returnData = dmService.ChangeOwnership(ownerData);

            if (returnData.sizeOfPartialErrors() > 0)
            {

                throw new Exception("修改所有者失败"+ returnData.GetPartialError(0).Messages[0]);

            }
        }

        public ModelObject createEmptyFile(String datasetType, String datasetName, String filePath, String fileRefName)
        {
            var dmService = DataManagementService.getService(Session.getConnection());
            FileManagementUtility fmsFileManagement = new FileManagementUtility(Session.getConnection(), null, null, new[] { "http://TC12:4544" }, "D:\\");
            // Create a Dataset
            DatasetProperties2 props = new DatasetProperties2();
            //props.ClientId = "datasetWriteTixTestClientId";
            //props.Type = "Text";
            //props.Name = "Sample-FMS-Upload-maxtt";
            //props.Description = "Testing put File";
            props.ClientId = datasetName  + "ClientId";
            props.Type = datasetType;
            props.Name = datasetName ;
            props.Description = "";
            DatasetProperties2[] currProps = { props };
            CreateDatasetsResponse resp = dmService.CreateDatasets2(currProps);

            // Create a file to associate with dataset
            var file = new FileInfo(filePath);

            if (!file.Exists)
                throw new Exception("要上传的文件不存在，上传文件失败！");

            DatasetFileInfo fileInfo = new DatasetFileInfo();
            fileInfo.ClientId = "file_1";
            //fileInfo.FileName = "./template/url.txt";
            fileInfo.FileName = file.FullName;
            fileInfo.NamedReferencedName = fileRefName;
            fileInfo.IsText = true;
            fileInfo.AllowReplace = false;

            DatasetFileInfo[] fileInfos = { fileInfo };

            GetDatasetWriteTicketsInputData inputData = new GetDatasetWriteTicketsInputData();
            inputData.Dataset = resp.Output[0].Dataset;
            inputData.CreateNewVersion = false;
            inputData.DatasetFileInfos = fileInfos;

            GetDatasetWriteTicketsInputData[] inputs = { inputData };

            ServiceData response = fmsFileManagement.PutFiles(inputs);

            if (response.sizeOfPartialErrors() > 0)
                System.Console.Out.WriteLine("FileManagementService single upload returned partial errrors: " + response.sizeOfPartialErrors());


            //add relation
            ModelObject datasets =  inputs[0].Dataset ;

            

            return datasets;
        }


        private void createRelations(ModelObject modObjprimary, ModelObject modObjSecondary, String sRelationName)
        {
            var dmService = DataManagementService.getService(Session.getConnection());


            Relationship[] relationShips = new Relationship[1];
            relationShips[0] = new Relationship();
            relationShips[0].ClientId = "One" + (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            relationShips[0].PrimaryObject = modObjprimary;
            relationShips[0].SecondaryObject = modObjSecondary;
            relationShips[0].RelationType = sRelationName;
            CreateRelationsResponse response = dmService.CreateRelations(relationShips);
            if (response.ServiceData.sizeOfPartialErrors() > 0)
            {
                //ProcessServiceDataForPartialErrors(response.serviceData);
            }
        }
    }   

        
}

