﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Reflection;
using System.Data.SqlClient;
using System.Data;
using System.Data.OleDb;
using System.Xml;
using System.Globalization;

using WanTai.DataModel;
using WanTai.DataModel.Configuration;
using WanTai.Common;
using iTextSharp.text;
using System.IO;
using iTextSharp.text.pdf;

namespace WanTai.Controller.PCR
{
    public class PCRTestResultViewListController
    {
        public List<RotationInfo> GetPCRResultRotation()
        {
            List<RotationInfo> recordList = new List<RotationInfo>();
            try
            {
                string connectionString = WanTai.Common.Configuration.GetConnectionString();
                //string commandText = "  select RotationID, RotationName from  RotationInfo"+
                //    " where RotationID in ( select distinct RotationID FROM PCRTestResult WHERE ExperimentID = @ExperimentID)";
                string commandText = "  select RotationID, RotationName from  RotationInfo" +
                    " where ExperimentID = @ExperimentID";
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand(commandText, conn))
                    {
                        cmd.Parameters.AddWithValue("@ExperimentID", SessionInfo.ExperimentID);

                        using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                        {
                            while (reader.Read())
                            {
                                RotationInfo info = new RotationInfo();
                                info.RotationID = (Guid)reader.GetValue(0);
                                info.RotationName = reader.GetValue(1).ToString();
                                recordList.Add(info);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                string errorMessage = e.Message + System.Environment.NewLine + e.StackTrace;
                LogInfoController.AddLogInfo(LogInfoLevelEnum.Error, errorMessage, SessionInfo.LoginName, this.GetType().ToString() + "->GetPCRResultRotation", SessionInfo.ExperimentID);
                throw;
            }

            return recordList;
        }

        public List<Plate> GetPCRPlateList(Guid rotationId, Guid experimentId)
        {
            try
            {
                using (WanTaiEntities entities = new WanTaiEntities())
                {
                    var records = entities.Plates.Where(c => c.RotationID == rotationId && c.ExperimentID == experimentId && c.PlateType == (short)PlateType.PCR_Plate);
                    if (records.Count() > 0)
                    {
                        return records.ToList<Plate>();
                    }
                }
            }
            catch (Exception e)
            {
                string errorMessage = e.Message + System.Environment.NewLine + e.StackTrace;
                LogInfoController.AddLogInfo(LogInfoLevelEnum.Error, errorMessage, SessionInfo.LoginName, this.GetType().ToString() + "->GetPCRPlateList", experimentId);
                throw;
            }

            return null;
        }

        public int GetSampleNumber(Guid experimentID)
        {
            try
            {
                using (WanTaiEntities entities = new WanTaiEntities())
                {
                    return entities.Tubes.Where(t => t.ExperimentID == experimentID && t.TubeType == 0 ).Count();
                }
            }
            catch (Exception e)
            {
                string errorMessage = e.Message + System.Environment.NewLine + e.StackTrace;
                LogInfoController.AddLogInfo(LogInfoLevelEnum.Error, errorMessage, SessionInfo.LoginName, this.GetType().ToString() + "->GetSampleNumber", experimentID);
                throw;
            }
        }

        public void QueryTubesPCRTestResult(Guid experimentId, Guid rotationId, DataTable dataTable, System.Collections.Generic.Dictionary<int, string> liquidTypeDictionary, System.Windows.Media.Color redColor, System.Windows.Media.Color greenColor, out string resultMessage)
        {
            //try
            {
                DataTable baseTable = dataTable.Clone();
                DataTable middTable = dataTable.Clone();

                Dictionary<Guid, string> tubeSampleCheckResult = new Dictionary<Guid, string>();
                List<ReagentSuppliesType> reagentSuppliesTypeList = WanTai.Common.Configuration.GetReagentSuppliesTypes();
                Dictionary<string, string> reagentSuppliesTypeDic = new Dictionary<string, string>();
                foreach (ReagentSuppliesType rType in reagentSuppliesTypeList)
                {
                    reagentSuppliesTypeDic.Add(rType.TypeId, rType.TypeName);
                }

                Dictionary<string, string> testingItemCheckResultDic = new Dictionary<string, string>();
                List<Guid> SampleTrackingIDList = new List<Guid>();
                List<string> SampleTrackingSampleIDList = new List<string>();

                resultMessage = string.Empty;
                string connectionString = WanTai.Common.Configuration.GetConnectionString();
                int OperationSequence = 1;
                int PCR_Liquid_Plate_itemtype = 205;
                string PCRLiquidPlateBarCode = null;
                //check 1.mix-DW96Plate sample tracking, if the volume < required volume, record it in memory
                string commandText_Mix = "select TubeID, SampleTracking.ItemID from View_Tubes_PCRPlatePosition left join SampleTracking on"
                    + " View_Tubes_PCRPlatePosition.BarCode = SampleTracking.SampleID and"
                    + " View_Tubes_PCRPlatePosition.RotationID = SampleTracking.RotationID and"
                    + " View_Tubes_PCRPlatePosition.ExperimentID = SampleTracking.ExperimentsID and"
                    + " View_Tubes_PCRPlatePosition.DWPlateBarCode = SampleTracking.RackID and"
                    + " SampleTracking.OperationSequence=@OperationSequence"
                    + " WHERE View_Tubes_PCRPlatePosition.RotationID=@RotationID"
                    + " and View_Tubes_PCRPlatePosition.ExperimentID = @ExperimentID"
                    + " and (SampleTracking.ItemID is null or View_Tubes_PCRPlatePosition.volume>ceiling(SampleTracking.CONCENTRATION*SampleTracking.VOLUME))";

                string commandText_AddLiquidToMix = "select TubeID, EnglishName, SampleID from View_Tubes_PCRPlatePosition"
                    + " inner join ( Select * from ( select SampleTracking.SampleID, SUM(ceiling(SampleTracking.CONCENTRATION*SampleTracking.VOLUME)) as volume,"
                    + " ReagentAndSuppliesConfiguration.EnglishName, SampleTracking.Position, ReagentAndSuppliesConfiguration.SimpleTrackingVolumn as SimpleTrackingVolumn"
                    + " from SampleTracking left join ReagentAndSuppliesConfiguration"
                    + " on charindex(ReagentAndSuppliesConfiguration.BarcodePrefix, SampleTracking.SampleID)=1"
                    + " and ReagentAndSuppliesConfiguration.ItemType=0 WHERE SampleTracking.RotationID=@RotationID"
                    + " and SampleTracking.OperationSequence=@OperationSequence group by SampleTracking.SampleID, ReagentAndSuppliesConfiguration.EnglishName,"
                    + " SampleTracking.Position, ReagentAndSuppliesConfiguration.SimpleTrackingVolumn) as a"
                    + " where volume<SimpleTrackingVolumn ) as s on View_Tubes_PCRPlatePosition.DWPosition = dbo.ChangeCharacterToPositionNumber(s.Position)"
                    + " where  View_Tubes_PCRPlatePosition.RotationID=@RotationID"
                    + " and View_Tubes_PCRPlatePosition.ExperimentID = @ExperimentID";

                string commandText_PCRLiquid = "select SampleTracking.ItemID, r.ItemType from SampleTracking inner join"
                    + " (select ReagentsAndSuppliesConsumption.Volume, ReagentsAndSuppliesConsumption.RotationID, ReagentAndSupplies.BarCode,"
                    + " ReagentAndSupplies.ItemType from ReagentsAndSuppliesConsumption inner join ReagentAndSupplies"
                    + " on ReagentsAndSuppliesConsumption.ReagentAndSupplieID = ReagentAndSupplies.ItemID and ReagentsAndSuppliesConsumption.VolumeType=0) as r"
                    + " on SampleTracking.RotationID = r.RotationID and SampleTracking.SampleID = @PCRLiquidPlateBarCode +'_'+r.BarCode"
                    + " where SampleTracking.RotationID=@RotationID"
                    + " and SampleTracking.ExperimentsID = @ExperimentID"
                    + " and SampleTracking.OperationSequence=@OperationSequence"
                    + " and ceiling(ABS((SampleTracking.CONCENTRATION*SampleTracking.VOLUME))) < r.Volume";

                string commandText_FenZhuang = "select View_Tubes_PCRPlatePosition.TubeID, SampleTracking.ItemID from View_Tubes_PCRPlatePosition left join SampleTracking on"
                    + " View_Tubes_PCRPlatePosition.PCRPlateBarcode = SampleTracking.RackID and View_Tubes_PCRPlatePosition.PCRPosition = dbo.ChangeCharacterToPositionNumber(SampleTracking.Position)"
                    + " and View_Tubes_PCRPlatePosition.RotationID = SampleTracking.RotationID"
                    + " and SampleTracking.CavityID = SampleTracking.SampleID"
                    + " and SampleTracking.OperationSequence = @OperationSequence"
                    + " where View_Tubes_PCRPlatePosition.RotationID =@RotationID"
                    + " and View_Tubes_PCRPlatePosition.ExperimentID = @ExperimentID"
                    + " and (SampleTracking.ItemID is null or (SampleTracking.CONCENTRATION = 0 and ABS(SampleTracking.VOLUME) < 50 ))";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    //1 混样
                    using (SqlCommand cmd = new SqlCommand(commandText_Mix, conn))
                    {
                        cmd.Parameters.AddWithValue("@RotationID", rotationId);
                        cmd.Parameters.AddWithValue("@ExperimentID", experimentId);
                        cmd.Parameters.AddWithValue("@OperationSequence", OperationSequence);

                        using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                        {
                            while (reader.Read())
                            {
                                if (reader["TubeID"] != DBNull.Value && !tubeSampleCheckResult.ContainsKey((Guid)reader["TubeID"]))
                                {
                                    tubeSampleCheckResult.Add((Guid)reader["TubeID"], "混样血管吸取量不足");
                                }

                                if (reader["ItemID"] != DBNull.Value)
                                {
                                    SampleTrackingIDList.Add((Guid)reader["ItemID"]);
                                }
                            }
                        }
                    }
                    //2 加试剂
                    OperationSequence = 2;
                    using (SqlCommand cmd = new SqlCommand(commandText_AddLiquidToMix, conn))
                    {
                        cmd.Parameters.AddWithValue("@RotationID", rotationId);
                        cmd.Parameters.AddWithValue("@ExperimentID", experimentId);
                        cmd.Parameters.AddWithValue("@OperationSequence", OperationSequence);

                        using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                        {
                            while (reader.Read())
                            {
                                if (!tubeSampleCheckResult.ContainsKey((Guid)reader["TubeID"]))
                                {
                                    tubeSampleCheckResult.Add((Guid)reader["TubeID"], "试剂" + reader["EnglishName"].ToString() + "吸取量不足");
                                }
                                else
                                {
                                    string message = tubeSampleCheckResult[(Guid)reader["TubeID"]];
                                    if (message.IndexOf("试剂" + reader["EnglishName"].ToString() + "吸取量不足") < 0)
                                        tubeSampleCheckResult[(Guid)reader["TubeID"]] = message + Environment.NewLine + "试剂" + reader["EnglishName"].ToString() + "吸取量不足";
                                }

                                if (reader["SampleID"] != DBNull.Value && !SampleTrackingSampleIDList.Contains((string)reader["SampleID"]))
                                {
                                    SampleTrackingSampleIDList.Add((string)reader["SampleID"]);
                                }
                            }
                        }
                    }

                    //2 PCR配液-get PCRLiquidPlateBarCode
                    OperationSequence = 2;
                    string selectCommand = "SELECT BarcodePrefix FROM ReagentAndSuppliesConfiguration WHERE ItemType=@ItemType";
                    using (SqlCommand cmd = new SqlCommand(selectCommand, conn))
                    {
                        cmd.Parameters.AddWithValue("@ItemType", PCR_Liquid_Plate_itemtype);

                        using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                        {
                            if (reader.Read())
                            {
                                PCRLiquidPlateBarCode = reader[0].ToString();
                            }
                        }
                    }

                    //check if can find the PCRLiquidPlate sampletracking file, if not find, then all pcr liquid is invalid,//todo
                    //2 PCR配液- get PCR liquid and its testing item is not enough
                    using (SqlCommand cmd = new SqlCommand(commandText_PCRLiquid, conn))
                    {
                        cmd.Parameters.AddWithValue("@PCRLiquidPlateBarCode", PCRLiquidPlateBarCode);
                        cmd.Parameters.AddWithValue("@RotationID", rotationId);
                        cmd.Parameters.AddWithValue("@ExperimentID", experimentId);
                        cmd.Parameters.AddWithValue("@OperationSequence", OperationSequence);

                        using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                        {
                            while (reader.Read())
                            {
                                string typeName = reagentSuppliesTypeDic[reader["ItemType"].ToString()];
                                if (!testingItemCheckResultDic.ContainsKey(typeName))
                                {
                                    testingItemCheckResultDic.Add(typeName, typeName + "配液添加量不够");
                                }

                                SampleTrackingIDList.Add((Guid)reader["ItemID"]);
                            }
                        }
                    }

                    //4试剂模板分装
                    OperationSequence = 4;
                    using (SqlCommand cmd = new SqlCommand(commandText_FenZhuang, conn))
                    {
                        cmd.Parameters.AddWithValue("@RotationID", rotationId);
                        cmd.Parameters.AddWithValue("@ExperimentID", experimentId);
                        cmd.Parameters.AddWithValue("@OperationSequence", OperationSequence);

                        using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                        {
                            while (reader.Read())
                            {
                                if (!tubeSampleCheckResult.ContainsKey((Guid)reader["TubeID"]))
                                {
                                    tubeSampleCheckResult.Add((Guid)reader["TubeID"], "试剂模板分装吸取量不够");
                                }
                                else
                                {
                                    string message = tubeSampleCheckResult[(Guid)reader["TubeID"]];
                                    if (message.IndexOf("试剂模板分装吸取量不够") < 0)
                                        tubeSampleCheckResult[(Guid)reader["TubeID"]] = message + Environment.NewLine + "试剂模板分装吸取量不够";
                                }

                                if (reader["ItemID"] != DBNull.Value)
                                {
                                    SampleTrackingIDList.Add((Guid)reader["ItemID"]);
                                }
                            }
                        }
                    }

                    string updateCommand = "UPDATE SampleTracking SET IsKeeping=1 where ItemID=@ItemID";
                    using (SqlCommand cmd = new SqlCommand(updateCommand, conn))
                    {
                        foreach (Guid itemId in SampleTrackingIDList)
                        {
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@ItemID", itemId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    updateCommand = "UPDATE SampleTracking SET IsKeeping=1 where SampleID=@SampleID";
                    using (SqlCommand cmd = new SqlCommand(updateCommand, conn))
                    {
                        foreach (string SampleID in SampleTrackingSampleIDList)
                        {
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@SampleID", SampleID);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                string commandText = "SELECT distinct TubeID, View_Tubes_PCRPlatePosition.BarCode, View_Tubes_PCRPlatePosition.Position,"
                    + " Grid, TubeType, PoolingRulesName, TestName, PCRPlateBarcode, PCRPlateID, PCRPosition, PCRTestResult.Result,"
                    + " PCRTestResult.ItemID FROM View_Tubes_PCRPlatePosition left join PCRTestResult"
                    + " on View_Tubes_PCRPlatePosition.RotationID = PCRTestResult.RotationID"
                    + " and View_Tubes_PCRPlatePosition.PCRPosition = PCRTestResult.Position"
                    + " and View_Tubes_PCRPlatePosition.PCRPlateID = PCRTestResult.PlateID"
                    + " WHERE View_Tubes_PCRPlatePosition.RotationID=@RotationID order by PCRPlateID, PCRPosition";
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand(commandText, conn))
                    {
                        cmd.Parameters.AddWithValue("@RotationID", rotationId);

                        using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                        {
                            WanTaiEntities _WanTaiEntities = new WanTaiEntities(); 
                            int index = 1;
                            while (reader.Read())
                            {
                                System.Data.DataRow dRow = baseTable.NewRow();
                                dRow["Number"] = index;

                                dRow["TubeID"] = reader["TubeID"];
                                if (reader["BarCode"] != DBNull.Value)
                                {
                                    dRow["TubeBarCode"] = reader["BarCode"].ToString();
                                }

                                dRow["TubePosition"] = "Tube" + reader["Grid"].ToString() + "_" + reader["Position"].ToString();
                                dRow["TubeType"] = reader["TubeType"];
                                if ((int)dRow["TubeType"] == (int)Tubetype.PositiveControl)
                                {
                                    dRow["TubeTypeColor"] = liquidTypeDictionary[(int)dRow["TubeType"]];
                                    dRow["TubeTypeColorVisible"] = "Visible";

                                    if (reader["TestName"] != DBNull.Value && reader["TestName"].ToString() == "M")
                                    {
                                        if (reader["Result"] != DBNull.Value &&
                                            !string.IsNullOrEmpty(reader["Result"].ToString()) && reader["Result"].ToString().Split(new string[] { PCRTest.PositiveResult }, StringSplitOptions.None).Length - 1 != 3)
                                        {
                                            resultMessage = "本批样品检测结果不成立，实验需重新测试";
                                        }
                                    }
                                    else
                                    {
                                        if (reader["Result"] != DBNull.Value &&
                                            !string.IsNullOrEmpty(reader["Result"].ToString()) && reader["Result"].ToString() != PCRTest.PositiveResult)
                                        {
                                            resultMessage = "本批样品检测结果不成立，实验需重新测试";
                                        }
                                    }
                                }
                                else if ((int)dRow["TubeType"] == (int)Tubetype.NegativeControl)
                                {
                                    dRow["TubeTypeColor"] = liquidTypeDictionary[(int)dRow["TubeType"]];
                                    dRow["TubeTypeColorVisible"] = "Visible";

                                    if (reader["TestName"] != DBNull.Value && reader["TestName"].ToString() == "M")
                                    {
                                        if (reader["Result"] != DBNull.Value &&
                                            !string.IsNullOrEmpty(reader["Result"].ToString()) && reader["Result"].ToString().Split(new string[] { PCRTest.NegativeResult }, StringSplitOptions.None).Length - 1 != 3)
                                        {
                                            resultMessage = "本批样品检测结果不成立，实验需重新测试";
                                        }
                                    }
                                    else
                                    {
                                        if (reader["Result"] != DBNull.Value &&
                                            !string.IsNullOrEmpty(reader["Result"].ToString()) && reader["Result"].ToString() != PCRTest.NegativeResult)
                                        {
                                            resultMessage = "本批样品检测结果不成立，实验需重新测试";
                                        }
                                    }
                                }
                                else
                                {
                                    dRow["TubeTypeColorVisible"] = "Hidden";
                                }

                                dRow["PoolingRuleName"] = reader["PoolingRulesName"];
                                dRow["TestingItemName"] = reader["TestName"];
                                if (reader["PCRPlateBarcode"] != DBNull.Value)
                                {
                                    dRow["PCRPlateBarCode"] = reader["PCRPlateBarcode"];
                                }

                                if (reader["PCRPosition"] != DBNull.Value)
                                {
                                    dRow["PCRPosition"] = reader["PCRPosition"];

                                    // previousPCRPosition = (int)dRow["PCRPosition"];
                                }

                                if (reader["ItemID"] != DBNull.Value)
                                {
                                    dRow["PCRTestItemID"] = reader["ItemID"];
                                }

                                dRow["Color"] = System.Windows.Media.Colors.White;

                                string itemID = reader["ItemID"] != DBNull.Value ? reader["ItemID"].ToString() : "";
                                PCRTestResult testResult = itemID != "" ? _WanTaiEntities.PCRTestResults.Where(Result => Result.ItemID == new Guid(itemID)).FirstOrDefault() : null;
                                
                                if (null != testResult)
                                {
                                    XmlDocument xdoc = new XmlDocument();
                                    xdoc.LoadXml(testResult.PCRContent);
                                    XmlNodeList nodelist = xdoc.SelectSingleNode("PCRContent").SelectNodes("Row");
                                    foreach (XmlNode node in nodelist)
                                    {
                                        dRow["PCRName"] = node.SelectSingleNode("SampleName").InnerText;
                                        string Detector = node.SelectSingleNode("Detector").InnerText;
                                        float ctNumber = 0;
                                        string ctString = node.SelectSingleNode("Ct").InnerText;
                                        if (float.TryParse(node.SelectSingleNode("Ct").InnerText, out ctNumber))
                                        {
                                            ctString = ctNumber.ToString("F2");//.Format("{0:E2}", myNumber); 
                                        }
                                        if (dRow["TestingItemName"].ToString() == "HBV")
                                        {
                                            if (SessionInfo.WorkDeskType == "100")
                                            {
                                                if ("VIC" == Detector || "HEX" == Detector)
                                                {
                                                    dRow["HBV"] = ctString;
                                                }
                                                else if ("ROX" == Detector)
                                                {
                                                    dRow["HBVIC"] = ctString;
                                                }
                                            }
                                            else
                                            {
                                                if ("VIC" == Detector || "HEX" == Detector)
                                                {
                                                    dRow["HBV"] = ctString;
                                                }
                                                else if ("ROX" == Detector)
                                                {
                                                    dRow["HBVIC"] = ctString;
                                                }
                                            }
                                        }
                                        else if (dRow["TestingItemName"].ToString() == "HCV")
                                        {
                                            if (SessionInfo.WorkDeskType == "100")
                                            {
                                                if ("ROX" == Detector)
                                                {
                                                    dRow["HCV"] = ctString;
                                                }
                                                else if ("FAM" == Detector)
                                                {
                                                    dRow["HCVIC"] = ctString;
                                                }
                                            }
                                            else
                                            {
                                                if ("FAM" == Detector)
                                                {
                                                    dRow["HCV"] = ctString;
                                                }
                                                else if ("ROX" == Detector)
                                                {
                                                    dRow["HCVIC"] = ctString;
                                                }
                                            }
                                        }
                                        else if (dRow["TestingItemName"].ToString() == "HIV")
                                        {
                                            if (SessionInfo.WorkDeskType == "100")
                                            {
                                                if ("FAM" == Detector)
                                                {
                                                    dRow["HIV"] = ctString;
                                                }
                                                else if ("VIC" == Detector || "HEX" == Detector)
                                                {
                                                    dRow["HIVIC"] = ctString;
                                                }
                                            }
                                            else
                                            {
                                                if ("FAM" == Detector)
                                                {
                                                    dRow["HIV"] = ctString;
                                                }
                                                else if ("ROX" == Detector)
                                                {
                                                    dRow["HIVIC"] = ctString;
                                                }
                                            }
                                        }
                                    }
                                }
                                if (reader["Result"] != DBNull.Value)
                                {
                                    dRow["PCRTestResult"] = reader["Result"];

                                    if (reader["Result"].ToString().Contains(PCRTest.PositiveResult))
                                    {
                                        dRow["Color"] = WantagColor.WantagRed;
                                    }
                                    else if (reader["Result"].ToString() == PCRTest.InvalidResult || reader["Result"].ToString() == PCRTest.NoResult)
                                    {
                                        dRow["Color"] = WantagColor.WantagYellow;
                                    }

                                    if (SessionInfo.WorkDeskType == "100"
                                        && dRow["PCRTestResult"].ToString() != PCRTest.InvalidResult
                                        && dRow["PCRTestResult"].ToString() != PCRTest.NegativeResult
                                        && dRow["PCRTestResult"].ToString() != PCRTest.PositiveResult
                                        && dRow["PCRTestResult"].ToString() != PCRTest.NoResult)
                                    {
                                        float myNumber = 0;
                                        if (float.TryParse(dRow["PCRTestResult"].ToString(), out myNumber))
                                        {
                                            dRow["PCRTestResult"] = myNumber.ToString("E2");//.Format("{0:E2}", myNumber); 
                                        }
                                    }
                                    dRow["PCRTestResult"] += ((SessionInfo.WorkDeskType == "100"
                                        && (dRow["PCRTestResult"].ToString() != PCRTest.InvalidResult
                                        && dRow["PCRTestResult"].ToString() != PCRTest.NegativeResult
                                        && dRow["PCRTestResult"].ToString() != PCRTest.PositiveResult
                                        && dRow["PCRTestResult"].ToString() != PCRTest.NoResult
                                        && "No Ct" != dRow["PCRTestResult"].ToString() 
                                        && "N/A" != dRow["PCRTestResult"].ToString() 
                                        && "" != dRow["PCRTestResult"].ToString().Trim()
                                        && "Undetermined" != dRow["PCRTestResult"].ToString()))
                                        ? ((dRow["TestingItemName"].ToString() == "HBV" || dRow["TestingItemName"].ToString() == "HCV") ? " IU/ml" : " cps/ml") : "");
                                }

                                if (tubeSampleCheckResult.ContainsKey((Guid)reader["TubeID"]))
                                {
                                    dRow["SimpleTrackingResult"] = tubeSampleCheckResult[(Guid)reader["TubeID"]];
                                    if (!string.IsNullOrEmpty(dRow["SimpleTrackingResult"].ToString()))
                                    {
                                        // dRow["Color"] = n new Color(232, 231, 154);
                                        // dRow["Color"] = System.Windows.Media.Colors.Yellow; 
                                        dRow["Color"] = WantagColor.WantagYellow; //System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(232, 231, 154));
                                        // dRow["Color"] = System.Windows.Media.Colors.Yellow; 
                                    }
                                }

                                if (testingItemCheckResultDic.ContainsKey((string)reader["TestName"]))
                                {
                                    if (dRow["SimpleTrackingResult"] != null)
                                        dRow["SimpleTrackingResult"] = dRow["SimpleTrackingResult"] + Environment.NewLine + testingItemCheckResultDic[(string)reader["TestName"]];
                                    else
                                        dRow["SimpleTrackingResult"] = testingItemCheckResultDic[(string)reader["TestName"]];
                                }

                                index++;
                                baseTable.Rows.Add(dRow);
                            }
                        }
                    }
                }

                // get pcr position dict
                Dictionary<int, List<DataRow>> pcrPosRows = new Dictionary<int, List<DataRow>>();
                foreach (DataRow baseRow in baseTable.Rows)
                {
                    int pcrPos = Int32.Parse(baseRow["PCRPosition"].ToString()); 
                    if (!pcrPosRows.ContainsKey(pcrPos))
                    {
                        pcrPosRows.Add(pcrPos, new List<DataRow>());
                    }
                    pcrPosRows[pcrPos].Add(baseRow);
                }
                // get midd table
                foreach (KeyValuePair<int, List<DataRow>> pcrPosRow in pcrPosRows)
                {
                    int listIndex = 0;
                    System.Data.DataRow middRow = middTable.NewRow();
                    foreach (DataRow baseRow in pcrPosRow.Value)
                    {
                        if (listIndex == 0)
                        {
                            middRow.ItemArray = baseRow.ItemArray;
                            middTable.Rows.Add(middRow);
                        }
                        else
                        {
                            middRow["TubeBarCode"] += "\n" + baseRow["TubeBarCode"];
                            middRow["TubePosition"] += "\n" + baseRow["TubePosition"];
                        }
                        listIndex++;
                    }
                }
                // get midd dictionary
                Dictionary<string, List<DataRow>> pcrTubeRows = new Dictionary<string, List<DataRow>>();
                foreach (DataRow middRow in middTable.Rows)
                {
                    string tubePos = (string)middRow["TubePosition"] + "-" + (string)middRow["PoolingRuleName"];
                    if (!pcrTubeRows.ContainsKey(tubePos))
                    {
                        pcrTubeRows.Add(tubePos, new List<DataRow>());
                    }
                    pcrTubeRows[tubePos].Add(middRow);
                }
                // get data table
                int dataIndex = 1;
                dataTable.Columns[11].DataType = typeof(string);
                foreach (KeyValuePair<string, List<DataRow>> pcrTubeRow in pcrTubeRows)
                {
                    int listIndex = 0;
                    System.Data.DataRow dataRow = dataTable.NewRow();
                    foreach (DataRow middRow in pcrTubeRow.Value)
                    {
                        if (listIndex == 0)
                        {
                            dataRow.ItemArray = middRow.ItemArray;
                            dataRow["Number"] = dataIndex;
                            dataRow["PCRTestResult"] = middRow["TestingItemName"] + " " + dataRow["PCRTestResult"];
                            dataTable.Rows.Add(dataRow);
                        }
                        else
                        {
                            dataRow["TestingItemName"] += "\n" + middRow["TestingItemName"];
                            dataRow["PCRPosition"] += "\n" + middRow["PCRPosition"];
                            dataRow["PCRTestResult"] += "\n" + middRow["TestingItemName"] + " " + middRow["PCRTestResult"];
                            dataRow["HBV"] += "\n" + middRow["HBV"];
                            dataRow["HBVIC"] += "\n" + middRow["HBVIC"];
                            dataRow["HCV"] += "\n" + middRow["HCV"];
                            dataRow["HCVIC"] += "\n" + middRow["HCVIC"];
                            dataRow["HIV"] += "\n" + middRow["HIV"];
                            dataRow["HIVIC"] += "\n" + middRow["HIVIC"];

                        }
                        listIndex++;
                    }
                    dataIndex++;
                }
                /*
                _pcrTable.Columns.Add("Number", typeof(int));
                _pcrTable.Columns.Add("Color", typeof(string));
                _pcrTable.Columns.Add("TubeID", typeof(Guid));
                _pcrTable.Columns.Add("TubeBarCode", typeof(string));
                _pcrTable.Columns.Add("TubePosition", typeof(string));
                _pcrTable.Columns.Add("TubeType", typeof(int));
                _pcrTable.Columns.Add("TubeTypeColor", typeof(string));
                _pcrTable.Columns.Add("TubeTypeColorVisible", typeof(string));
                _pcrTable.Columns.Add("PoolingRuleName", typeof(string));
                _pcrTable.Columns.Add("TestingItemName", typeof(string));
                _pcrTable.Columns.Add("PCRPlateBarCode", typeof(string));
                _pcrTable.Columns.Add("PCRPosition", typeof(int));
                _pcrTable.Columns.Add("PCRTestItemID", typeof(Guid));
                _pcrTable.Columns.Add("PCRTestResult", typeof(string));
                _pcrTable.Columns.Add("PCRTestContent", typeof(string));
                _pcrTable.Columns.Add("SimpleTrackingResult", typeof(string));
                 */
            }
            //catch (Exception e)
           // {
            //    string errorMessage = e.Message + System.Environment.NewLine + e.StackTrace;
            //    LogInfoController.AddLogInfo(LogInfoLevelEnum.Error, errorMessage, SessionInfo.LoginName, this.GetType().ToString() + "->QueryTubesPCRTestResult", SessionInfo.ExperimentID);
            //    throw;
           // }
        }

        public bool DeleteRotationPCRTestResult(Guid rotationId)
        {
            try
            {
                string connectionString = WanTai.Common.Configuration.GetConnectionString();
                string commandText = "DELETE FROM  PCRTestResult WHERE RotationID=@RotationID";
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand(commandText, conn))
                    {
                        cmd.Parameters.AddWithValue("@RotationID", rotationId);

                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }

            }
            catch (Exception e)
            {
                string errorMessage = e.Message + System.Environment.NewLine + e.StackTrace;
                LogInfoController.AddLogInfo(LogInfoLevelEnum.Error, errorMessage, SessionInfo.LoginName, this.GetType().ToString() + "->DeleteRotationPCRTestResult", SessionInfo.ExperimentID);
                return false;
            }
        }

        public bool CheckRotationHasPCRTestResult(Guid rotationId, Guid experimentId)
        {
            try
            {
                using (WanTaiEntities entities = new WanTaiEntities())
                {
                    var records = entities.PCRTestResults.Where(c => c.RotationID == rotationId && c.ExperimentID == experimentId);
                    return records.Count() > 0 ? true : false;
                }

            }
            catch (Exception e)
            {
                string errorMessage = e.Message + System.Environment.NewLine + e.StackTrace;
                LogInfoController.AddLogInfo(LogInfoLevelEnum.Error, errorMessage, SessionInfo.LoginName, this.GetType().ToString() + "->CheckRotationHasPCRTestResult", SessionInfo.ExperimentID);
                throw;
            }
        }

        public void QueryTubesPCRTestResultByBarcode(string barcode, DataTable dataTable, System.Collections.Generic.Dictionary<int, string> liquidTypeDictionary)
        {
            try
            {
                string connectionString = WanTai.Common.Configuration.GetConnectionString();

                string commandText = "SELECT distinct TubeID, A.BarCode, A.Position, TubeType, PoolingRulesName, TestName, PCRTestResult.Result,"
                    + " Grid, PCRTestResult.ItemID, A.ExperimentID, A.ExperimentName, A.StartTime, A.EndTime, A.LoginName  FROM ( "
                    + " SELECT View_Tubes_PCRPlatePosition.*, ExperimentsInfo.ExperimentName, ExperimentsInfo.StartTime, ExperimentsInfo.EndTime,"
                    + " ExperimentsInfo.LoginName FROM View_Tubes_PCRPlatePosition inner join ExperimentsInfo on View_Tubes_PCRPlatePosition.ExperimentID=ExperimentsInfo.ExperimentID) AS A"
                    + " left join PCRTestResult on A.RotationID = PCRTestResult.RotationID and A.PCRPosition = PCRTestResult.Position and A.PCRPlateID = PCRTestResult.PlateID"
                    + " WHERE A.BarCode=@Barcode order by A.StartTime desc";
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand(commandText, conn))
                    {
                        cmd.Parameters.AddWithValue("@Barcode", barcode);

                        using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                        {
                            int index = 1;
                            while (reader.Read())
                            {
                                System.Data.DataRow dRow = dataTable.NewRow();
                                dRow["Number"] = index;

                                dRow["TubeID"] = reader["TubeID"];
                                if (reader["BarCode"] != DBNull.Value)
                                {
                                    dRow["TubeBarCode"] = reader["BarCode"].ToString();
                                }
                                dRow["TubePosition"] = "Tube" + reader["Grid"].ToString() + "_" + reader["Position"].ToString();
                                dRow["TubeType"] = reader["TubeType"];
                                if ((int)dRow["TubeType"] == (int)Tubetype.PositiveControl)
                                {
                                    dRow["TubeTypeColor"] = liquidTypeDictionary[(int)dRow["TubeType"]];
                                    dRow["TubeTypeColorVisible"] = "Visible";
                                }
                                else if ((int)dRow["TubeType"] == (int)Tubetype.NegativeControl)
                                {
                                    dRow["TubeTypeColor"] = liquidTypeDictionary[(int)dRow["TubeType"]];
                                    dRow["TubeTypeColorVisible"] = "Visible";
                                }
                                else
                                {
                                    dRow["TubeTypeColorVisible"] = "Hidden";
                                }

                                dRow["PoolingRuleName"] = reader["PoolingRulesName"];
                                dRow["TestingItemName"] = reader["TestName"];

                                if (reader["ItemID"] != DBNull.Value)
                                {
                                    dRow["PCRTestItemID"] = reader["ItemID"];
                                }

                                if (reader["Result"] != DBNull.Value)
                                {
                                    dRow["PCRTestResult"] = reader["Result"];
                                }

                                if (reader["ExperimentID"] != DBNull.Value)
                                {
                                    dRow["ExperimentID"] = reader["ExperimentID"];
                                }

                                if (reader["ExperimentName"] != DBNull.Value)
                                {
                                    dRow["ExperimentName"] = reader["ExperimentName"];
                                }

                                if (reader["StartTime"] != DBNull.Value)
                                {
                                    dRow["StartTime"] = reader["StartTime"];
                                }

                                if (reader["EndTime"] != DBNull.Value)
                                {
                                    dRow["EndTime"] = reader["EndTime"];
                                }

                                if (reader["LoginName"] != DBNull.Value)
                                {
                                    dRow["LoginName"] = reader["LoginName"];
                                }
                                index++;
                                dataTable.Rows.Add(dRow);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                string errorMessage = e.Message + System.Environment.NewLine + e.StackTrace;
                LogInfoController.AddLogInfo(LogInfoLevelEnum.Error, errorMessage, SessionInfo.LoginName, this.GetType().ToString() + "->QueryTubesPCRTestResultByBarcode", SessionInfo.ExperimentID);
                throw;
            }
        }

        public bool SaveExcelFile(string fileName, Guid experimentId)
        {
            try
            {
                ConfigRotationController rotationController = new ConfigRotationController();
                List<RotationInfo> rotationList = rotationController.GetCurrentRotationInfos(experimentId);

                DataTable _pcrTable = new DataTable();
                _pcrTable.Columns.Add("Number", typeof(int));
                _pcrTable.Columns.Add("Color", typeof(string));
                _pcrTable.Columns.Add("TubeID", typeof(Guid));
                _pcrTable.Columns.Add("TubeBarCode", typeof(string));
                _pcrTable.Columns.Add("TubePosition", typeof(string));
                _pcrTable.Columns.Add("TubeType", typeof(int));
                _pcrTable.Columns.Add("PCRName", typeof(string));
                _pcrTable.Columns.Add("TubeTypeColor", typeof(string));
                _pcrTable.Columns.Add("TubeTypeColorVisible", typeof(string));
                _pcrTable.Columns.Add("PoolingRuleName", typeof(string));
                _pcrTable.Columns.Add("TestingItemName", typeof(string));
                _pcrTable.Columns.Add("PCRPlateBarCode", typeof(string));
                _pcrTable.Columns.Add("PCRPosition", typeof(string));
                _pcrTable.Columns.Add("HBV", typeof(string));
                _pcrTable.Columns.Add("HBVIC", typeof(string));
                _pcrTable.Columns.Add("HCV", typeof(string));
                _pcrTable.Columns.Add("HCVIC", typeof(string));
                _pcrTable.Columns.Add("HIV", typeof(string));
                _pcrTable.Columns.Add("HIVIC", typeof(string));
                _pcrTable.Columns.Add("PCRTestItemID", typeof(Guid));
                _pcrTable.Columns.Add("PCRTestResult", typeof(string));
                _pcrTable.Columns.Add("PCRTestContent", typeof(string));
                _pcrTable.Columns.Add("SimpleTrackingResult", typeof(string));

                System.Collections.Generic.Dictionary<int, string> liquidTypeDictionary = new System.Collections.Generic.Dictionary<int, string>();
                List<LiquidType> LiquidTypeList = SessionInfo.LiquidTypeList = WanTai.Common.Configuration.GetLiquidTypes();
                foreach (LiquidType liquidType in LiquidTypeList)
                {
                    liquidTypeDictionary.Add(liquidType.TypeId, liquidType.Color);
                }

                string errorMessage = string.Empty;

                ExperimentsInfo expInfo = new WanTai.Controller.HistoryQuery.ExperimentsController().GetExperimentById(experimentId);
                string experimentName = expInfo.ExperimentName;
                //string fileName = folderPath + "\\" + experimentName + "_" + DateTime.Now.ToString("yyyyMMdd") + ".xls";
                if (System.IO.File.Exists(fileName))
                    System.IO.File.Delete(fileName);

                WanTai.Controller.PCR.PCRTestResultViewListController pcrController = new WanTai.Controller.PCR.PCRTestResultViewListController();

                string extension = System.IO.Path.GetExtension(fileName);
                int refIndex = 1;

                if (extension.Equals(".pdf"))
                {
                    DataSet ds = new DataSet();
                    //ds.DataSetName = "实验名称：" + experimentName + "   操作员：" + experimentLoginName + " 运行时间：" + experimentCreateDate;
                    foreach (RotationInfo rotation in rotationList)
                    {
                        _pcrTable.Clear();
                        pcrController.QueryTubesPCRTestResult(experimentId, rotation.RotationID, _pcrTable, liquidTypeDictionary, System.Windows.Media.Colors.Red, System.Windows.Media.Colors.Green, out errorMessage);

                        //DataTable dt = new DataTable("     " + rotation.RotationName + "     操作员：" + experimentLoginName + " 创建时间：" + rotation.CreateTime.ToString("yyyy年MM月dd日HH时mm分ss秒"));
                        DataTable dt = new DataTable(rotation.RotationID + "," + rotation.RotationName);
                        dt.Columns.Add("序号");
                        dt.Columns.Add("类型");
                        dt.Columns.Add("样本名称");
                        dt.Columns.Add("样本条码");
                        dt.Columns.Add("样本位置");
                        dt.Columns.Add("检测方式");
                        dt.Columns.Add("检测项目");
                        dt.Columns.Add("PCR板条码");
                        dt.Columns.Add("PCR孔位");
                        dt.Columns.Add("HBV");
                        dt.Columns.Add("HBVIC");
                        dt.Columns.Add("HCV");
                        dt.Columns.Add("HCVIC");
                        dt.Columns.Add("HIV");
                        dt.Columns.Add("HIVIC");
                        dt.Columns.Add("检测结果");
                        dt.Columns.Add("实验记录");
                        dt.Columns.Add("Color");

                        foreach (DataRow row in _pcrTable.Rows)
                        {
                            int tubeType = (int)row["TubeType"];
                            string tubeTypeStr = string.Empty;
                            if (tubeType == (int)Tubetype.PositiveControl)
                            {
                                if (SessionInfo.WorkDeskType == "100")
                                {
                                    tubeTypeStr = row["TestingItemName"].ToString() + " PC";
                                }
                                else
                                {
                                    tubeTypeStr = "PC";
                                }
                            }
                            else if (tubeType == (int)Tubetype.NegativeControl)
                            {
                                if (SessionInfo.WorkDeskType == "100")
                                {
                                    tubeTypeStr = row["TestingItemName"].ToString() + " NC";
                                }
                                else
                                {
                                    tubeTypeStr = "NC";
                                }
                            }
                            else if (tubeType == (int)Tubetype.Complement)
                            {
                                if (SessionInfo.WorkDeskType == "100")
                                {
                                    if (refIndex == 5)
                                        refIndex = 1;
                                    tubeTypeStr = row["TestingItemName"].ToString() + " 定量参考品" + refIndex;
                                    refIndex ++;
                                }
                                else
                                {
                                    tubeTypeStr = "补液";
                                }
                            }
                            else
                            {
                                tubeTypeStr = "样本";
                            }

                            DataRow dr = dt.NewRow();
                            dr["序号"] = (int)row["Number"];
                            dr["类型"] = tubeTypeStr;
                            dr["样本名称"] = row["PCRName"].ToString();
                            dr["样本条码"] = row["TubeBarCode"].ToString();
                            dr["样本位置"] = row["TubePosition"].ToString();
                            dr["检测方式"] = row["PoolingRuleName"].ToString();
                            dr["检测项目"] = row["TestingItemName"].ToString();
                            dr["PCR板条码"] = row["PCRPlateBarCode"].ToString();
                            dr["PCR孔位"] = row["PCRPosition"].ToString();
                            dr["HBV"] = row["HBV"].ToString().Replace("Undetermined", "No Ct");
                            dr["HBVIC"] = row["HBVIC"].ToString().Replace("Undetermined", "No Ct");
                            dr["HCV"] = row["HCV"].ToString().Replace("Undetermined", "No Ct");
                            dr["HCVIC"] = row["HCVIC"].ToString().Replace("Undetermined", "No Ct");
                            dr["HIV"] = row["HIV"].ToString().Replace("Undetermined", "No Ct");
                            dr["HIVIC"] = row["HIVIC"].ToString().Replace("Undetermined", "No Ct");
                            dr["检测结果"] = row["PCRTestResult"].ToString();
                            dr["实验记录"] = row["SimpleTrackingResult"].ToString();
                            dr["Color"] = row["Color"];
                            dt.Rows.Add(dr);
                        }
                        ds.Tables.Add(dt);
                    }

                    return ExportToPdf(ds, fileName, expInfo);
                }
                else if (extension.Equals(".xls"))
                {

                    using (OleDbConnection connection = new OleDbConnection("Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + fileName + ";Extended Properties=\"Excel 8.0;HDR=YES;\""))
                    {
                        connection.Open();
                        OleDbCommand command = new OleDbCommand();
                        command.Connection = connection;

                        foreach (RotationInfo rotation in rotationList)
                        {
                            _pcrTable.Clear();
                            pcrController.QueryTubesPCRTestResult(experimentId, rotation.RotationID, _pcrTable, liquidTypeDictionary, System.Windows.Media.Colors.Red, System.Windows.Media.Colors.Green, out errorMessage);

                            string createTableSql = "create table [" + rotation.RotationName + "] ([序号] Integer,[类型] nvarchar, [样本名称] nvarchar,[样本条码] nvarchar,[样本位置] nvarchar,"
                                + "[检测方式] nvarchar,[检测项目] nvarchar,[PCR板条码] nvarchar,[PCR孔位] nvarchar,[HBV] nvarchar,[HBVIC] nvarchar,[HCV] nvarchar,[HCVIC] nvarchar,[HIV] nvarchar,[HIVIC] nvarchar,[检测结果] nvarchar,[实验记录] nvarchar)";
                            command.CommandText = createTableSql;
                            command.ExecuteNonQuery();

                            string insertSql = string.Empty;
                            foreach (DataRow row in _pcrTable.Rows)
                            {
                                int tubeType = (int)row["TubeType"];
                                string tubeTypeStr = string.Empty;
                                if (tubeType == (int)Tubetype.PositiveControl)
                                {
                                    if (SessionInfo.WorkDeskType == "100")
                                    {
                                        tubeTypeStr = row["TestingItemName"].ToString() + " PC";
                                    }
                                    else
                                    {
                                        tubeTypeStr = "PC";
                                    }
                                }
                                else if (tubeType == (int)Tubetype.NegativeControl)
                                {
                                    if (SessionInfo.WorkDeskType == "100")
                                    {
                                        tubeTypeStr = row["TestingItemName"].ToString() + " NC";
                                    }
                                    else
                                    {
                                        tubeTypeStr = "NC";
                                    }
                                }
                                else if (tubeType == (int)Tubetype.Complement)
                                {
                                    if (SessionInfo.WorkDeskType == "100")
                                    {
                                        if (refIndex == 5)
                                            refIndex = 1;
                                        tubeTypeStr = row["TestingItemName"].ToString() + " 定量参考品" + refIndex;
                                        refIndex++;
                                    }
                                    else
                                    {
                                        tubeTypeStr = "补液";
                                    }
                                }
                                else
                                {
                                    tubeTypeStr = "样本";
                                }

                                insertSql = string.Format("Insert into [" + rotation.RotationName + "] (序号,类型,样本名称,样本条码,样本位置,检测方式,检测项目,PCR板条码,PCR孔位,HBV,HBVIC,HCV,HCVIC,HIV,HIVIC,检测结果,实验记录) "
                                    + "values({0},'{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}', '{11}','{12}','{13}','{14}','{15}','{16}')",
                                        (int)row["Number"],
                                        tubeTypeStr,
                                        row["PCRName"].ToString(),
                                        row["TubeBarCode"].ToString(),
                                        row["TubePosition"].ToString(),
                                        row["PoolingRuleName"].ToString(),
                                        row["TestingItemName"].ToString(),
                                        row["PCRPlateBarCode"].ToString(),
                                        row["PCRPosition"].ToString(),
                                        row["HBV"].ToString(),
                                        row["HBVIC"].ToString(),
                                        row["HCV"].ToString(),
                                        row["HCVIC"].ToString(),
                                        row["HIV"].ToString(),
                                        row["HIVIC"].ToString(),
                                        row["PCRTestResult"].ToString(),
                                        row["SimpleTrackingResult"].ToString());
                                command.CommandText = insertSql;
                                command.ExecuteNonQuery();
                            }
                        }

                        connection.Close();
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                throw e;
            }

        }

        #region ExportToPdf()

        /// <summary>
        /// Exporttopdf
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private bool ExportToPdf(DataSet ds, string fileName, ExperimentsInfo expInfo)
        {
            ///设置导出字体
            string FontPath = "C://WINDOWS//Fonts//msyh.ttf"; //"C://WINDOWS//Fonts//simsun.ttc,1";
            int FontSize = 8;

            Boolean result = true;
            //竖排模式,大小为A4，四周边距均为25
            Document document = new Document(PageSize.A4.Rotate(), 25, 25, 20, 20);

            //调用PDF的写入方法流
            //注意FileMode-Create表示如果目标文件不存在，则创建，如果已存在，则覆盖。
            PdfWriter writer = PdfWriter.GetInstance(document, new FileStream(fileName, FileMode.Create));

            //创建PDF文档中的字体
            BaseFont baseFont = BaseFont.CreateFont(
              FontPath,
              BaseFont.IDENTITY_H,
              BaseFont.NOT_EMBEDDED);

            //根据字体路径和字体大小属性创建字体
            iTextSharp.text.Font font = new iTextSharp.text.Font(baseFont, FontSize);
            iTextSharp.text.Font fontWhite = new iTextSharp.text.Font(baseFont, FontSize);
            fontWhite.Color = iTextSharp.text.Color.WHITE;

            iTextSharp.text.Font fontTitle = new iTextSharp.text.Font(baseFont, 8);

            iTextSharp.text.Font fontSmall = new iTextSharp.text.Font(baseFont, 4);

            iTextSharp.text.Font fontWanTag = new iTextSharp.text.Font(baseFont, 12);
            fontWanTag.IsBold();

            DateTime dTime = DateTime.Now;

            HeaderFooter footer = new HeaderFooter(new Phrase("导出时间：" + dTime.ToString("yyyy/MM/dd HH:mm:ss") + "    页数: "), true);
            footer.Border = Rectangle.NO_BORDER;
            footer.Alignment = Element.ALIGN_RIGHT;
            document.Footer = footer;

            //HeaderFooter Header = new HeaderFooter(new Phrase("WanTag 实验报告"),false);
            //Header.Border = Rectangle.NO_BORDER;
            //Header.Alignment = Element.ALIGN_CENTER;
            //document.Header = Header;

            //打开目标文档对象
            document.Open();

            // document.Add(new Paragraph("\n"));

            Paragraph p_WanTag = new Paragraph("WanTag 实验报告", fontWanTag);
            p_WanTag.Alignment = Element.ALIGN_RIGHT;
            document.Add(p_WanTag);

            document.Add(new Paragraph("\n"));

            // document.Add(new Paragraph("\n"));

            Paragraph p_expName = new Paragraph(("实验名称：" + expInfo.ExperimentName).PadRight(44, ' ') + "操 作 员：" + expInfo.LoginName, fontTitle);
            document.Add(p_expName);

            Paragraph p_expLoginName = new Paragraph(("样本数量：" + GetSampleNumber(expInfo.ExperimentID)).PadRight(55, ' ') + "实验时间：" + expInfo.StartTime.ToString("yyyy/MM/dd HH:mm:ss") + "--" + Convert.ToDateTime(expInfo.EndTime).ToString("yyyy/MM/dd HH:mm:ss"), fontTitle);
            document.Add(p_expLoginName);

            for (int k = 0; k < ds.Tables.Count; k++)
            {
                if (k > 0)
                {
                    document.NewPage();
                }

                string PCRTimeString = "";
                string PCRDeviceString = "";
                string PCRBarCodeString = "";
                string[] rotationArr = ds.Tables[k].TableName.Split(new char[] {','});
                string rotationID = rotationArr[0];
                string rotationName = rotationArr[1];

                List<Plate> plateList = GetPCRPlateList(new Guid(rotationID), expInfo.ExperimentID);
                if (plateList != null && plateList.Count > 0)
                {
                    foreach (Plate plate in plateList)
                    {
                        PCRBarCodeString += PCRBarCodeString == "" ? plate.BarCode : ", " + plate.BarCode;
                        XmlDocument xdoc = new XmlDocument();
                        xdoc.LoadXml(plate.PCRContent);
                        XmlNode node = xdoc.SelectSingleNode("PCRContent");
                        PCRDeviceString += PCRDeviceString == "" ? node.SelectSingleNode("PCRDevice").InnerText : ", " + node.SelectSingleNode("PCRDevice").InnerText;
                        string timeString = node.SelectSingleNode("PCRStartTime").InnerText + "--" + node.SelectSingleNode("PCREndTime").InnerText;
                        PCRTimeString += PCRTimeString == "" ? timeString : ", " + timeString;
                    }
                }

                Paragraph p_Rotation = new Paragraph(rotationName.PadRight(62, ' ') + "扩增时间：" + PCRTimeString, fontTitle);
                document.Add(p_Rotation);

                Paragraph p_PCR = new Paragraph(("PCR仪：" + PCRDeviceString ).PadRight(53, ' ') + "PCR条码：" + PCRBarCodeString, fontTitle);
                document.Add(p_PCR);

                if (ds.Tables[k].Rows[0]["检测结果"].ToString().Contains("重新测定") || ds.Tables[k].Rows[1]["检测结果"].ToString().Contains("重新测定"))
                {
                    Paragraph p_QC = new Paragraph( "QC: 质控品结果不符合标准，实验无效", fontTitle);
                    document.Add(p_QC);
                }

                document.Add(new Paragraph("\n", fontSmall));

                //根据数据表内容创建一个PDF格式的表
                PdfPTable table = new PdfPTable(ds.Tables[k].Columns.Count - 1);
                table.WidthPercentage = 100f;
                table.SetTotalWidth(new float[] { 3, 10, 8, 15, 12, 8, 6, 12, 6, 6, 6, 6, 6, 6, 6, 20, 30 });
                // 添加表头，每一页都有表头
                for (int j = 0; j < ds.Tables[k].Columns.Count - 1; j++)
                {
                    string cellName = ds.Tables[k].Columns[j].ColumnName;
                    cellName = cellName.Replace("BarCode", "条码").Replace("Position", "孔位");
                    if (cellName == "HBV" || cellName == "HBVIC" || cellName == "HCV"
                        || cellName == "HCVIC" || cellName == "HIV" || cellName == "HIVIC")
                    {
                        continue;
                    }
                    PdfPCell cell = new PdfPCell(new Phrase(cellName, font));

                    // cell.UseAscender = true;
                    cell.FixedHeight = 30;
                    cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.BackgroundColor = new Color(220, 220, 220);
                    cell.Rowspan = 2;

                    table.AddCell(cell);

                    if (cellName == "PCR孔位")
                    {
                        cell = new PdfPCell(new Phrase("结果数据（Ct）", font));

                        // cell.UseAscender = true;
                        cell.FixedHeight = 15;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.BackgroundColor = new Color(220, 220, 220);

                        cell.Colspan = 6;
                        table.AddCell(cell);
                    }
                }
                for (int j = 0; j < ds.Tables[k].Columns.Count - 1; j++)
                {
                    string cellName = ds.Tables[k].Columns[j].ColumnName;
                    cellName = cellName.Replace("BarCode", "条码").Replace("Position", "孔位");
                    if (cellName == "HBV" || cellName == "HBVIC" || cellName == "HCV"
                        || cellName == "HCVIC" || cellName == "HIV" || cellName == "HIVIC")
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(cellName, font));

                        // cell.UseAscender = true;
                        cell.FixedHeight = 15;
                        cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.BackgroundColor = new Color(220, 220, 220);

                        table.AddCell(cell);
                    }
                }


                // 告诉程序这行是表头，这样页数大于1时程序会自动为你加上表头。
                table.HeaderRows = 1;

                //遍历原datatable的数据行
                for (int i = 0; i < ds.Tables[k].Rows.Count; i++)
                {
                    for (int j = 0; j < ds.Tables[k].Columns.Count - 1; j++)
                    {
                        try
                        {
                            PdfPCell cell = new PdfPCell(new Phrase(ds.Tables[k].Rows[i][j].ToString(), font));
                            System.Drawing.Color Color = System.Drawing.ColorTranslator.FromHtml(ds.Tables[k].Rows[i]["Color"].ToString());
                            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                            cell.HorizontalAlignment = Element.ALIGN_CENTER;
                            cell.BackgroundColor = new iTextSharp.text.Color(Color);
                            if (ds.Tables[k].Rows[i]["Color"].ToString() == WantagColor.WantagRed)
                                cell.Phrase = new Phrase(ds.Tables[k].Rows[i][j].ToString(), fontWhite);
                            if (ds.Tables[k].Columns[j].ColumnName == "类型")
                            {
                                if (ds.Tables[k].Rows[i][j].ToString().Contains("NC")) {
                                    cell.BackgroundColor = new iTextSharp.text.Color(System.Drawing.ColorTranslator.FromHtml(WantagColor.WantagGreen));
                                    cell.Phrase = new Phrase(ds.Tables[k].Rows[i][j].ToString(), fontWhite);
                                }
                                else if (ds.Tables[k].Rows[i][j].ToString().Contains("PC"))
                                {
                                    cell.BackgroundColor = new iTextSharp.text.Color(System.Drawing.ColorTranslator.FromHtml(WantagColor.WantagRed));
                                    cell.Phrase = new Phrase(ds.Tables[k].Rows[i][j].ToString(), fontWhite);
                                }
                            }

                            table.AddCell(cell);

                            // table.AddCell(new Phrase(ds.Tables[k].Rows[i][j].ToString(), font));
                        }
                        catch (Exception e)
                        {
                            result = false;
                        }
                    }
                }

                //在目标文档中添加转化后的表数据
                document.Add(table);
            }


            //关闭目标文件
            document.Close();

            //关闭写入流
            writer.Close();
            return result;
        }


        #endregion
    }
}
