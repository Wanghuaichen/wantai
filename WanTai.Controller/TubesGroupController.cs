﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using WanTai.DataModel;
using System.Data;
using System.IO;
namespace WanTai.Controller
{
    public class TubesGroupController
    {
        public TubesBatch SaveTubesGroup(Guid ExperimentID, TubesBatch DelTubesBatch, int BatchIndex, IList<TubeGroup> TubeGroupList, DataTable Tubes, out int ErrType, out string ErrMsg)
        {
            try
            {
                ErrMsg = "操作成功！";
                ErrType = 0;
               // ExperimentID = new Guid("1C188E0B-F8A7-11E0-A935-0019D147C478");
                using (WanTaiEntities _WanTaiEntities = new WanTaiEntities())
                {
                    #region 删除上次操作的数据
                    if (DelTubesBatch.TubesBatchID != new Guid())
                    {
                        TubesBatch tubeBatch = _WanTaiEntities.TubesBatches.Where(_TubeBatch => _TubeBatch.TubesBatchID == DelTubesBatch.TubesBatchID).FirstOrDefault();
                        if (tubeBatch != null)
                        {
                            List<TubeGroup> DelTubeGroups = _WanTaiEntities.TubeGroups.Where(DelTubeGroup => DelTubeGroup.TubesBatchID == (Guid)DelTubesBatch.TubesBatchID).ToList<TubeGroup>();
                            foreach (TubeGroup DelTubeGroup in DelTubeGroups)
                            {
                                List<Tube> DelTubes = _WanTaiEntities.Tubes.Where(DelTube => DelTube.TubeGroupID == DelTubeGroup.TubeGroupID).ToList<Tube>();
                                foreach (Tube DelTube in DelTubes)
                                {
                                    DelTube.DWPlatePositions.Clear();
                                    _WanTaiEntities.Tubes.DeleteObject(DelTube);
                                  //  _WanTaiEntities.SaveChanges();
                                }
                                DelTubeGroup.TestingItemConfigurations.Clear();
                          //      _WanTaiEntities.SaveChanges();
                                _WanTaiEntities.TubeGroups.DeleteObject(DelTubeGroup);
                          //      _WanTaiEntities.SaveChanges();
                                //  List<TestingItemConfiguration> DelTestingItemConfigurations=_WanTaiEntities.TestingItemConfigurations.Where(DelTestingItemConfiguration=>DelTestingItemConfiguration.)
                            }
                            List<Plate> DelPlates = _WanTaiEntities.Plates.Where(plate=>plate.TubesBatchID==tubeBatch.TubesBatchID).ToList();
                            foreach(Plate plate in DelPlates)
                            {
                                List<PCRPlatePosition> DelPCRPlatePosition = _WanTaiEntities.PCRPlatePositions.Where(Position => Position.PlateID==plate.PlateID).ToList();
                                foreach (PCRPlatePosition position in DelPCRPlatePosition)
                                {
                                    position.DWPlatePositions.Clear();
                                    _WanTaiEntities.PCRPlatePositions.DeleteObject(position);
                                }
                                List<DWPlatePosition> DelDWPlatePosition = _WanTaiEntities.DWPlatePositions.Where(Position => Position.PlateID == plate.PlateID).ToList();
                                foreach (DWPlatePosition position in DelDWPlatePosition)
                                {
                                    position.Tubes.Clear();
                                    position.PCRPlatePositions.Clear();
                                    _WanTaiEntities.DWPlatePositions.DeleteObject(position);
                                }
                                _WanTaiEntities.Plates.DeleteObject(plate);
                            }

                            _WanTaiEntities.TubesBatches.DeleteObject(tubeBatch);
                           // _WanTaiEntities.SaveChanges();
                        }
                    }
                    #endregion
                   // _WanTaiEntities.SaveChanges();
                    #region 保存TubesBatch
                    TubesBatch _TubesBatch = new TubesBatch();
                    _TubesBatch.ExperimentID = ExperimentID;
                    _TubesBatch.CreateTime = DateTime.Now;
                    _TubesBatch.TestingItem = DelTubesBatch.TestingItem;
                    _TubesBatch.TubesBatchID = WanTaiObjectService.NewSequentialGuid();
                    _TubesBatch.TubesBatchName = "批次" + BatchIndex.ToString();
                    _WanTaiEntities.AddToTubesBatches(_TubesBatch);
                    #endregion
                   // _WanTaiEntities.SaveChanges();
                    RotationInfo ExperimentRotation = _WanTaiEntities.RotationInfoes.Where(Rotation => Rotation.ExperimentID == ExperimentID && Rotation.TubesBatchID == null).FirstOrDefault();
                    string CSVPath = WanTai.Common.Configuration.GetWorkListFilePath();
                    string DWFileName = CSVPath  + WanTai.Common.Configuration.GetAddSamplesWorkListFileName();
                    //string PCRFileName = CSVPath + "PCR" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
                    ///todo: each test item should has a seperate file name, the file is in TestingItemConfiguration.
                    int PoolCountInTotal = 0;
                    int PoolingWorkListRowCount = 0;
                    #region 开始保存文件
                    using (FileStream DWFile = new FileStream(DWFileName, FileMode.Create, FileAccess.Write))
                    {
                        using (StreamWriter DWStreamWriter = new StreamWriter(DWFile, Encoding.Default))
                        {
                            #region 保存 Plates信息  96孔板 1\2 PCR配液板
                            DWStreamWriter.WriteLine("Source Labware Label,Source Position,Volume,Destination Labware Label,Destination Position");
                            //PCRStreamWriter.WriteLine("Source Labware Label,Source Position,Volume,Destination Labware Label,Destination Position");
                            ///DW板和PCR板
                            Plate DWPlate_1 = new Plate();
                            DWPlate_1.ExperimentID = ExperimentID;
                            DWPlate_1.PlateID = WanTaiObjectService.NewSequentialGuid();
                            DWPlate_1.PlateName = PlateName.DWPlate1;
                            DWPlate_1.TubesBatchID = _TubesBatch.TubesBatchID;
                            DWPlate_1.PlateType = (short)PlateType.Mix_Plate;
                            _WanTaiEntities.AddToPlates(DWPlate_1);

                            Plate DWPlate_2 = new Plate();
                            DWPlate_2.ExperimentID = ExperimentID;
                            DWPlate_2.PlateID = WanTaiObjectService.NewSequentialGuid();
                            DWPlate_2.PlateName = PlateName.DWPlate2;
                            DWPlate_2.PlateType = (short)PlateType.Mix_Plate;
                            DWPlate_2.TubesBatchID = _TubesBatch.TubesBatchID;
                            _WanTaiEntities.AddToPlates(DWPlate_2);
                            #endregion 
                            
                            #region 阴阳对应物 TubeGroup信息(单检)
                            TubeGroup NewTubeGroupNull = new TubeGroup();
                            NewTubeGroupNull.CreateTime = DateTime.Now;
                            NewTubeGroupNull.ExperimentID = _TubesBatch.ExperimentID;
                            NewTubeGroupNull.PoolingRulesID = _WanTaiEntities.PoolingRulesConfigurations.Where(PoolingRules => (PoolingRules.PoolingRulesName == "单检" && PoolingRules.TubeNumber == 1)).FirstOrDefault().PoolingRulesID;
                            //NewTubeGroupNull.TubesBatch = _TubesBatch;                        
                            //NewTubeGroupNull.PoolingRulesConfiguration = new PoolingRulesConfiguration() { PoolingRulesID = _TubeGroup.PoolingRulesID };
                            //NewTubeGroupNull.TestingItemConfigurations = new System.Data.Objects.DataClasses.EntityCollection<TestingItemConfiguration>();
                            //NewTubeGroupNull.TestingItemConfigurations = _TubeGroup.TestingItemConfigurations;
                            NewTubeGroupNull.isComplement = false;
                            //NewTubeGroup.PoolingRulesName = _TubeGroup.PoolingRulesName;
                            //NewTubeGroup.RowIndex = _TubeGroup.RowIndex;
                            //NewTubeGroup.TestintItemName = _TubeGroup.TestintItemName;
                            //NewTubeGroup.TubesPosition = _TubeGroup.TubesPosition;
                            //NewTubeGroup.TubesGroupName = _TubeGroup.TubesGroupName;
                            //NewTubeGroup.TubesNumber = _TubeGroup.TubesNumber;
                            //NewTubeGroup.TestingItemConfigurations = _TubeGroup.TestingItemConfigurations;
                            int TotalTestingItem = 0;
                            foreach (KeyValuePair<Guid, int> _TestingItem in DelTubesBatch.TestingItem)
                            {
                                if (_TestingItem.Value == 0) continue;
                                TotalTestingItem += _TestingItem.Value + 2;
                                NewTubeGroupNull.TestingItemConfigurations.Add(_WanTaiEntities.TestingItemConfigurations.Where(TestingItem => TestingItem.TestingItemID == _TestingItem.Key).FirstOrDefault());
                            }
                            //if (DelTubesBatch.HBVNumber > 0)
                            //    NewTubeGroupNull.TestingItemConfigurations.Add(_WanTaiEntities.TestingItemConfigurations.Where(TestingItem => TestingItem.TestingItemName == "HBV").FirstOrDefault());
                            //if (DelTubesBatch.HCVNumber > 0)
                            //    NewTubeGroupNull.TestingItemConfigurations.Add(_WanTaiEntities.TestingItemConfigurations.Where(TestingItem => TestingItem.TestingItemName == "HCV").FirstOrDefault());
                            //if (DelTubesBatch.HIVNumber > 0)
                            //    NewTubeGroupNull.TestingItemConfigurations.Add(_WanTaiEntities.TestingItemConfigurations.Where(TestingItem => TestingItem.TestingItemName == "HIV").FirstOrDefault());
                            NewTubeGroupNull.TubeGroupID = WanTaiObjectService.NewSequentialGuid();
                            NewTubeGroupNull.TubesBatchID = _TubesBatch.TubesBatchID;
                            _WanTaiEntities.AddToTubeGroups(NewTubeGroupNull);
                            #endregion
                            #region 保存阴阳对照物,
                           
                            string[] PCRCSV = new string[TotalTestingItem]; //PCR文件
                            //  string[] PCRCSV = new string[DelTubesBatch.HBVNumber + (DelTubesBatch.HBVNumber > 0 ? 2 : 0) + DelTubesBatch.HCVNumber + (DelTubesBatch.HCVNumber > 0 ? 2 : 0) + DelTubesBatch.HIVNumber + (DelTubesBatch.HIVNumber > 0 ? 2 : 0)];
                            //  Tubes.Select();
                            //阴、阳对照物   0血管、1补液、2阳性、3阴性  PositiveControl = 2, NegativeControl = 3
                            //SystemFluidConfiguration NegativeControl = _WanTaiEntities.SystemFluidConfigurations.Where(systemFluid => systemFluid.ItemType == 3).FirstOrDefault();
                            //SystemFluidConfiguration PositiveControl = _WanTaiEntities.SystemFluidConfigurations.Where(systemFluid => systemFluid.ItemType == 2).FirstOrDefault();
                            DWPlatePosition DWPlate_NegativeControl_1=new DWPlatePosition(); 
                            DWPlatePosition DWPlate_NegativeControl_2=new DWPlatePosition(); 
                            DWPlatePosition DWPlate_PositiveControl_1=new DWPlatePosition(); 
                            DWPlatePosition DWPlate_PositiveControl_2=new DWPlatePosition(); 
                            string[] NegativePositive=new string[4];
                            foreach (string str in Tubes.TableName.Split(']'))
                            {
                                if (string.IsNullOrEmpty(str)) continue;
                                string TubesPosition = str.Remove(0, 1);
                                int ColumnIndex = int.Parse(TubesPosition.Split(',')[0]);
                                int RowIndex = int.Parse(TubesPosition.Split(',')[1]) - 1;
                                Tube tube = new Tube();
                                tube.BarCode = Tubes.Rows[RowIndex]["BarCode" + ColumnIndex.ToString()].ToString();
                                tube.ExperimentID = ExperimentID;
                                tube.Position = RowIndex + 1;
                                tube.Grid = ColumnIndex;
                                tube.TubeGroupID = NewTubeGroupNull.TubeGroupID;
                                tube.TubeID = WanTaiObjectService.NewSequentialGuid();
                                tube.TubePosBarCode = Tubes.Rows[RowIndex]["TubePosBarCode" + ColumnIndex.ToString()].ToString();
                                if (Tubes.Rows[RowIndex]["TubeType" + ColumnIndex.ToString()].ToString() == "Complement")
                                    tube.TubeType = (int)Tubetype.Complement;
                                if (Tubes.Rows[RowIndex]["TubeType" + ColumnIndex.ToString()].ToString() == "NegativeControl")
                                    tube.TubeType = (int)Tubetype.NegativeControl;
                                if (Tubes.Rows[RowIndex]["TubeType" + ColumnIndex.ToString()].ToString() == "PositiveControl")
                                    tube.TubeType = (int)Tubetype.PositiveControl;
                                if (Tubes.Rows[RowIndex]["TubeType" + ColumnIndex.ToString()].ToString() == "Tube")
                                    tube.TubeType = (int)Tubetype.Tube;
                                tube.Volume = 480;

                                if (tube.TubeType == (int)Tubetype.PositiveControl || tube.TubeType == (int)Tubetype.NegativeControl)
                                {
                                    _WanTaiEntities.AddToTubes(tube);

                                    DWPlatePosition DWPositionA = new DWPlatePosition();
                                    DWPositionA.DWPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                    DWPositionA.TubeGroupID = NewTubeGroupNull.TubeGroupID;
                                    DWPositionA.Position = tube.TubeType == (int)Tubetype.PositiveControl?2:1;
                                    DWPositionA.PlateID = DWPlate_1.PlateID;
                                    DWPositionA.Tubes.Add(tube);
                                    // DWPlate_NegativeControl_1.Tubes.Add(_WanTaiEntities.Tubes.Where(t => t.TubeID == tube.TubeID).FirstOrDefault());
                                    _WanTaiEntities.AddToDWPlatePositions(DWPositionA);

                                    DWPlatePosition DWPositionB = new DWPlatePosition();
                                    DWPositionB.DWPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                    DWPositionB.TubeGroupID = NewTubeGroupNull.TubeGroupID;
                                    DWPositionB.Position = tube.TubeType == (int)Tubetype.PositiveControl ? 2 : 1;
                                    DWPositionB.PlateID = DWPlate_2.PlateID;
                                    DWPositionB.Tubes.Add(tube);
                                    // DWPlate_NegativeControl_1.Tubes.Add(_WanTaiEntities.Tubes.Where(t => t.TubeID == tube.TubeID).FirstOrDefault());
                                    _WanTaiEntities.AddToDWPlatePositions(DWPositionB);
                                    if (tube.TubeType == (int)Tubetype.PositiveControl)
                                    {
                                        DWPlate_PositiveControl_1 = DWPositionA;
                                        DWPlate_PositiveControl_2 = DWPositionB;
                                        NegativePositive[1] = "Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + ",480," + PlateName.DWPlate1 + ",2";
                                        NegativePositive[3] = "Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + ",480," + PlateName.DWPlate2 + ",2";
                                        //DWStreamWriter.WriteLine(tube.BarCode + "," + (RowIndex + 1).ToString() + ",480,"+PlateName.DWPlate1+",2");
                                        //DWStreamWriter.WriteLine(tube.BarCode + "," + (RowIndex + 1).ToString() + ",480,"+PlateName.DWPlate2+",2");
                                    }

                                    if (tube.TubeType == (int)Tubetype.NegativeControl)
                                    {
                                        DWPlate_NegativeControl_1 = DWPositionA;
                                        DWPlate_NegativeControl_2 = DWPositionB;
                                        NegativePositive[0] = "Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + ",480," + PlateName.DWPlate1 + ",1";
                                        NegativePositive[2] = "Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + ",480," + PlateName.DWPlate2 + ",1";
                                        //DWStreamWriter.WriteLine(tube.BarCode + "," + (RowIndex + 1).ToString() + ",480,"+PlateName.DWPlate1+",1");
                                        //DWStreamWriter.WriteLine(tube.BarCode + "," + (RowIndex + 1).ToString() + ",480,"+PlateName.DWPlate2+",1");

                                    }
                                    PoolingWorkListRowCount++;
                                    PoolingWorkListRowCount++;
                                   // HolePosition++;

                                }// NewTubeGroup.Tubes.Add(tube);
                            }
                            int HolePosition = 2;//96孔板起始位置
                            foreach (string str in NegativePositive)
                                DWStreamWriter.WriteLine(str);
                            #endregion 
                            //_WanTaiEntities.SaveChanges();
                            #region 保存阴阳对照物PCR(2011-11-22)
                            Dictionary<string, int> PoolCountOfTestItem = new Dictionary<string, int>();
                            List<TestingItemConfiguration> TestingItemList = _WanTaiEntities.TestingItemConfigurations.OrderBy(TestintItem => TestintItem.DisplaySequence).ToList();
                            int PCRPosition = 1;
                            int LastTestingItemCount=0;
                            Plate PCRPlate = null;
                            int PCRPosition_tmp = 1;
                            FormulaParameters formulaParameters = new FormulaParameters();
                            foreach (TestingItemConfiguration TestionItem in TestingItemList)
                            {
                                if (!DelTubesBatch.TestingItem.ContainsKey(TestionItem.TestingItemID) || DelTubesBatch.TestingItem[TestionItem.TestingItemID] == 0)
                                {
                                    PoolCountOfTestItem.Add(TestionItem.TestingItemName, 0);
                                    continue;
                                }
                                if (PCRPlate != null)
                                    TestionItem.PlateID = PCRPlate.PlateID;
                                if ((LastTestingItemCount + DelTubesBatch.TestingItem[TestionItem.TestingItemID] + 2) > 96 || PCRPlate==null)
                                {
                                    PCRPlate = new Plate();
                                    PCRPlate.ExperimentID = ExperimentID;
                                    PCRPlate.PlateID = WanTaiObjectService.NewSequentialGuid();
                                    PCRPlate.TubesBatchID = _TubesBatch.TubesBatchID;
                                    PCRPlate.PlateName = PlateName.PCRPlate;
                                    PCRPlate.PlateType = (short)PlateType.PCR_Plate;
                                    _WanTaiEntities.AddToPlates(PCRPlate);
                                    TestionItem.PlateID = PCRPlate.PlateID;
                                    LastTestingItemCount = 0;
                                    PCRPosition = 1;
                                    formulaParameters.PCRPlatesCount += 1;
                                }
                          
                                LastTestingItemCount += DelTubesBatch.TestingItem[TestionItem.TestingItemID] + 2;
                                PCRPlatePosition _PCRPlatePosition = new PCRPlatePosition();
                                _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                _PCRPlatePosition.PlateID = PCRPlate.PlateID;
                                _PCRPlatePosition.TestName = TestionItem.TestingItemName;
                                _PCRPlatePosition.Position = PCRPosition;
                                _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_1);
                                _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_2);

                                //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                                //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                                _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);

                                _PCRPlatePosition = new PCRPlatePosition();
                                _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                _PCRPlatePosition.PlateID = PCRPlate.PlateID;
                                _PCRPlatePosition.TestName = TestionItem.TestingItemName;
                                _PCRPlatePosition.Position = PCRPosition + 1;
                                _PCRPlatePosition.DWPlatePositions.Add(DWPlate_PositiveControl_1);
                                _PCRPlatePosition.DWPlatePositions.Add(DWPlate_PositiveControl_2);
                                //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                                //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                                _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);


                                PCRCSV[PCRPosition_tmp - 1] = PlateName.DWPlate5 + ",1,20," + PlateName.PCRPlate + "," + PCRPosition.ToString();
                                PCRCSV[PCRPosition_tmp] = PlateName.DWPlate5 + ",2,20," + PlateName.PCRPlate + "," + (PCRPosition + 1).ToString();
                                TestionItem.TestingItemPCR = PCRPosition_tmp+2;
                                TestionItem.TestingItemPosition = PCRPosition + 2;
                                PCRPosition += DelTubesBatch.TestingItem[TestionItem.TestingItemID] + 2;
                                PCRPosition_tmp += DelTubesBatch.TestingItem[TestionItem.TestingItemID] + 2;

                                formulaParameters.PCRWorkListRowCount += DelTubesBatch.TestingItem[TestionItem.TestingItemID] + 2;
                                PoolCountOfTestItem.Add(TestionItem.TestingItemName, DelTubesBatch.TestingItem[TestionItem.TestingItemID]);
                            }
                            //DataModel.FormulaParameters.PCRWorkListRowCount = PCRPosition - 1;
                            formulaParameters.PoolCountOfTestItem = PoolCountOfTestItem;
                            foreach (string strKey in formulaParameters.PoolCountOfTestItem.Keys)
                            {
                                if (formulaParameters.PoolCountOfTestItem[strKey] > 0)
                                    formulaParameters.TestItemCountInTotal++;
                            }
                            #endregion
                            #region 废除的代码
                            //foreach (KeyValuePair<Guid, int> _TestingItem in DelTubesBatch.TestingItem)
                            //{
                            //    TotalTestingItem += _TestingItem.Value + 2;
                            //    NewTubeGroupNull.TestingItemConfigurations.Add(_WanTaiEntities.TestingItemConfigurations.Where(TestingItem => TestingItem.TestingItemID == _TestingItem.Key).FirstOrDefault());
                            //}
                           

                            //int HBVPosition = 2;
                            //int HCVPosition = 2;
                            //int HIVPosition = 2;
                            //if (DelTubesBatch.HBVNumber > 0)
                            //{
                            //    PCRPlatePosition _PCRPlatePosition = new PCRPlatePosition();
                            //    _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                            //    _PCRPlatePosition.PlateID = PCRPlate.PlateID;
                            //    _PCRPlatePosition.TestName = "HBV";
                            //    _PCRPlatePosition.Position = 1;
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_1);
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_2);
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                            //    _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);

                            //    _PCRPlatePosition = new PCRPlatePosition();
                            //    _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                            //    _PCRPlatePosition.PlateID = PCRPlate.PlateID;
                            //    _PCRPlatePosition.TestName = "HBV";
                            //    _PCRPlatePosition.Position = 2;
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_1);
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_2);
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                            //    _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);


                            //    PCRCSV[0] = PlateName.DWPlate5+",1,20,"+PlateName.PCRPlate+",1";
                            //    PCRCSV[1] = PlateName.DWPlate5+",2,20,"+PlateName.PCRPlate+",2";
                            //}
                            //if (DelTubesBatch.HCVNumber > 0)
                            //{
                            //    if (DelTubesBatch.HBVNumber > 0)
                            //        HCVPosition = 2 + DelTubesBatch.HBVNumber + 2;


                            //    PCRPlatePosition _PCRPlatePosition = new PCRPlatePosition();
                            //    _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                            //    _PCRPlatePosition.PlateID = PCRPlate.PlateID;
                            //    _PCRPlatePosition.TestName = "HCV";
                            //    _PCRPlatePosition.Position = (HCVPosition - 1);
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_1);
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_2);
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                            //    _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);

                            //    _PCRPlatePosition = new PCRPlatePosition();
                            //    _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                            //    _PCRPlatePosition.PlateID = PCRPlate.PlateID;
                            //    _PCRPlatePosition.TestName = "HCV";
                            //    _PCRPlatePosition.Position = HCVPosition;
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_1);
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_2);
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                            //    _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);

                            //    PCRCSV[HCVPosition - 2] = PlateName.DWPlate5+",1,20,"+PlateName.PCRPlate+"," + (HCVPosition - 1).ToString();
                            //    PCRCSV[HCVPosition - 1] = PlateName.DWPlate5+",2,20,"+PlateName.PCRPlate+"," + HCVPosition.ToString();
                            //}
                            //if (DelTubesBatch.HIVNumber > 0)
                            //{
                            //    if (DelTubesBatch.HBVNumber > 0)
                            //        HIVPosition = 2 + DelTubesBatch.HBVNumber + 2;
                            //    if (DelTubesBatch.HCVNumber > 0)
                            //        HIVPosition = 2 + DelTubesBatch.HCVNumber + 2;

                            //    PCRPlatePosition _PCRPlatePosition = new PCRPlatePosition();
                            //    _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                            //    _PCRPlatePosition.PlateID = PCRPlate.PlateID;
                            //    _PCRPlatePosition.TestName = "HIV";
                            //    _PCRPlatePosition.Position = (HIVPosition - 1);
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_1);
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_2);
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                            //    _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);

                            //    _PCRPlatePosition = new PCRPlatePosition();
                            //    _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                            //    _PCRPlatePosition.PlateID = PCRPlate.PlateID;
                            //    _PCRPlatePosition.TestName = "HIV";
                            //    _PCRPlatePosition.Position = HIVPosition;
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_1);
                            //    _PCRPlatePosition.DWPlatePositions.Add(DWPlate_NegativeControl_2);
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                            //    //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                            //    _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);
                            //    PCRCSV[HIVPosition - 2] = PlateName.DWPlate5+",1,20,"+PlateName.PCRPlate+"," + (HIVPosition - 1).ToString();
                            //    PCRCSV[HIVPosition - 1] = PlateName.DWPlate5+",2,20,"+PlateName.PCRPlate+"," + HIVPosition.ToString();
                            //}
                            #endregion
                            List<SystemFluidConfiguration> SystemFluid = _WanTaiEntities.SystemFluidConfigurations.ToList().Where(systemFluidConfiguration => (int)systemFluidConfiguration.ItemType == 1).ToList();
                            int SystemFluidIndex = 0;
                          
                            //_WanTaiEntities.SaveChanges();
                            foreach (TubeGroup _TubeGroup in TubeGroupList)
                            {
                                #region 保存 TubeGroup
                                TubeGroup NewTubeGroup = new TubeGroup();
                                NewTubeGroup.CreateTime = DateTime.Now;
                                NewTubeGroup.ExperimentID = _TubesBatch.ExperimentID;
                                NewTubeGroup.PoolingRulesID = _TubeGroup.PoolingRulesID;
                                //NewTubeGroup.TubesBatch = _TubesBatch;                        
                                //NewTubeGroup.PoolingRulesConfiguration = new PoolingRulesConfiguration() { PoolingRulesID = _TubeGroup.PoolingRulesID };
                                //NewTubeGroup.TestingItemConfigurations = new System.Data.Objects.DataClasses.EntityCollection<TestingItemConfiguration>();
                                //NewTubeGroup.TestingItemConfigurations = _TubeGroup.TestingItemConfigurations;
                                NewTubeGroup.isComplement = _TubeGroup.isComplement;
                                NewTubeGroup.PoolingRulesName = _TubeGroup.PoolingRulesName;
                                NewTubeGroup.RowIndex = _TubeGroup.RowIndex;
                                NewTubeGroup.TestintItemName = _TubeGroup.TestintItemName;
                                NewTubeGroup.TubesPosition = _TubeGroup.TubesPosition;
                                NewTubeGroup.TubesGroupName = _TubeGroup.TubesGroupName;
                                NewTubeGroup.TubesNumber = _TubeGroup.TubesNumber;
                                //NewTubeGroup.TestingItemConfigurations = _TubeGroup.TestingItemConfigurations;
                                foreach (TestingItemConfiguration _TestingItemConfiguration in _TubeGroup.TestingItemConfigurations)
                                {
                                    //NewTubeGroup.TestingItemConfigurations.RelationshipSet.
                                    NewTubeGroup.TestingItemConfigurations.Add(_WanTaiEntities.TestingItemConfigurations.Where(p => p.TestingItemID == _TestingItemConfiguration.TestingItemID).FirstOrDefault());
                                    //NewTubeGroup.TestingItemConfigurations.Intersect(new TestingItemConfiguration() { TestingItemID = _TestingItemConfiguration.TestingItemID, TestingItemColor = _TestingItemConfiguration.TestingItemColor, TestingItemName = _TestingItemConfiguration.TestingItemName});
                                    //_TubeGroup.TestingItemConfigurations.i.AddObject(_TestingItemConfiguration);
                                }
                                NewTubeGroup.TubeGroupID = WanTaiObjectService.NewSequentialGuid();
                                NewTubeGroup.TubesBatchID = _TubesBatch.TubesBatchID;
                                _WanTaiEntities.AddToTubeGroups(NewTubeGroup);
                                #endregion
                                // _WanTaiEntities.SaveChanges();
                                // NewTubeGroup.TubesBatch = _TubesBatch;

                                //foreach (TestingItemConfiguration _TestingItemConfiguration in _TubeGroup.TestingItemList)
                                //{

                                //  //  _TubeGroup.TestingItemConfigurations.i.AddObject(_TestingItemConfiguration);
                                //}
                                //NewTubeGroup.Tubes = new System.Data.Objects.DataClasses.EntityCollection<Tube>();

                                PoolingRulesConfiguration PollingRules = _WanTaiEntities.PoolingRulesConfigurations.Where(PollingRule => PollingRule.PoolingRulesID == _TubeGroup.PoolingRulesID).FirstOrDefault();
                                float Volume = 960 / PollingRules.TubeNumber;
                                DWPlatePosition DWPlate_1_Position = new DWPlatePosition();
                                DWPlatePosition DWPlate_2_Position = new DWPlatePosition();
                                //PCRPlatePosition PCRPositionHBV = new PCRPlatePosition();
                                //PCRPlatePosition PCRPositionHCV = new PCRPlatePosition();
                                //PCRPlatePosition PCRPositionHIV = new PCRPlatePosition();
                                string[] TubesPosition = _TubeGroup.TubesPosition.Split(']');
                                if (PollingRules.TubeNumber == 1)
                                {
                                    #region 单检
                                    Volume = 480;
                                    StringBuilder ONETubesPosition2 = new StringBuilder();
                                    foreach (string str in TubesPosition)
                                    {
                                        #region 保存tube信息
                                        if (string.IsNullOrEmpty(str)) continue;
                                        string TubePosition = str.Remove(0, 1);
                                        int ColumnIndex = int.Parse(TubePosition.Split(',')[0]);
                                        int RowIndex = int.Parse(TubePosition.Split(',')[1]) - 1;
                                        Tube tube = new Tube();
                                        tube.BarCode = Tubes.Rows[RowIndex]["BarCode" + ColumnIndex.ToString()].ToString();
                                        tube.ExperimentID = ExperimentID;
                                        tube.Position = RowIndex + 1;
                                        tube.Grid = ColumnIndex;
                                        tube.TubeGroupID = NewTubeGroup.TubeGroupID;
                                        tube.TubeID = WanTaiObjectService.NewSequentialGuid();
                                        tube.TubePosBarCode = Tubes.Rows[RowIndex]["TubePosBarCode" + ColumnIndex.ToString()].ToString();
                                        tube.TubeType = (int)Tubetype.Tube;
                                        tube.Volume = Volume;
                                        #endregion

                                        DWPlate_1_Position = new DWPlatePosition();
                                        DWPlate_1_Position.DWPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                        DWPlate_1_Position.TubeGroupID = NewTubeGroup.TubeGroupID;
                                        DWPlate_1_Position.Position = HolePosition + 1;
                                        DWPlate_1_Position.PlateID = DWPlate_1.PlateID;

                                        DWPlate_2_Position = new DWPlatePosition();
                                        DWPlate_2_Position.DWPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                        DWPlate_2_Position.TubeGroupID = NewTubeGroup.TubeGroupID;
                                        DWPlate_2_Position.Position = HolePosition + 1;
                                        DWPlate_2_Position.PlateID = DWPlate_2.PlateID;


                                        //PCR文件
                                        foreach (TestingItemConfiguration TestionItem in TestingItemList)
                                        {
                                            if (_TubeGroup.TestingItemConfigurations.Where(_TestionItem => _TestionItem.TestingItemID == TestionItem.TestingItemID).Count() == 0)
                                                continue;
                                            //if (!DelTubesBatch.TestingItem.ContainsKey(TestionItem.TestingItemID)) continue;
                                            //if (DelTubesBatch.TestingItem[TestionItem.TestingItemID] == 0) continue;
                                            PCRPlatePosition _PCRPlatePosition = new PCRPlatePosition();
                                            _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                            _PCRPlatePosition.PlateID = TestionItem.PlateID;// PCRPlate.PlateID;
                                            _PCRPlatePosition.TestName = TestionItem.TestingItemName;
                                            _PCRPlatePosition.Position = TestionItem.TestingItemPosition;
                                            _PCRPlatePosition.DWPlatePositions.Add(DWPlate_1_Position);
                                            _PCRPlatePosition.DWPlatePositions.Add(DWPlate_2_Position);
                                            //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                                            //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                                            _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);

                                            PCRCSV[TestionItem.TestingItemPCR - 1] = PlateName.DWPlate5 + "," + (HolePosition + 1).ToString() + ",20," + PlateName.PCRPlate + "," + TestionItem.TestingItemPosition.ToString();
                                            TestionItem.TestingItemPosition += 1;
                                            TestionItem.TestingItemPCR += 1;
                                        }

                                        DWPlate_1_Position.Tubes.Add(tube);
                                        _WanTaiEntities.AddToDWPlatePositions(DWPlate_1_Position);

                                        DWPlate_2_Position.Tubes.Add(tube);
                                        _WanTaiEntities.AddToDWPlatePositions(DWPlate_2_Position);

                                        DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate1 + "," + (HolePosition + 1).ToString());
                                        if (ONETubesPosition2.Length > 0)
                                            ONETubesPosition2.Append(System.Environment.NewLine + "Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate2 + "," + (HolePosition + 1).ToString());
                                        else
                                            ONETubesPosition2.Append("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate2 + "," + (HolePosition + 1).ToString());
                                        HolePosition++;
                                        PoolingWorkListRowCount++;
                                        PoolingWorkListRowCount++;
                                    }
                                    DWStreamWriter.Write(ONETubesPosition2.ToString() + System.Environment.NewLine);
                                    #endregion
                                }
                                else
                                {
                                    int BalanceTubesCount = _TubeGroup.TubesNumber;//剩余
                                    int TubesPositionIndex=0; //
                                    int demand=PollingRules.TubeNumber  * 8;//需求量  48
                                    int demandIndex = 0;///需求量index
                                    StringBuilder ONETubesPosition2 = new StringBuilder();
                                    int tubeCount = 0;
                                    List<DWPlatePosition> DWPlate_1_PositionList = new List<DWPlatePosition>();
                                    List<DWPlatePosition> DWPlate_2_PositionList = new List<DWPlatePosition>();
                                    #region 按8个一组
                                    while (BalanceTubesCount  >= demand)
                                    {
                                        string str= TubesPosition[TubesPositionIndex];
                                        #region 保存tube信息
                                        if (string.IsNullOrEmpty(str)) continue;
                                        string TubePosition = str.Remove(0, 1);
                                        int ColumnIndex = int.Parse(TubePosition.Split(',')[0]);
                                        int RowIndex = int.Parse(TubePosition.Split(',')[1]) - 1;
                                        Tube tube = new Tube();
                                        tube.BarCode = Tubes.Rows[RowIndex]["BarCode" + ColumnIndex.ToString()].ToString();
                                        tube.ExperimentID = ExperimentID;
                                        tube.Position = RowIndex + 1;
                                        tube.Grid = ColumnIndex;
                                        tube.TubeGroupID = NewTubeGroup.TubeGroupID;
                                        tube.TubeID = WanTaiObjectService.NewSequentialGuid();
                                        tube.TubePosBarCode = Tubes.Rows[RowIndex]["TubePosBarCode" + ColumnIndex.ToString()].ToString();
                                        tube.TubeType = (int)Tubetype.Tube;
                                        tube.Volume = Volume;
                                        #endregion
                                        if (demand > PollingRules.TubeNumber / 2 * 8)
                                        {
                                            if (DWPlate_1_PositionList.Count < 8)
                                            {
                                                DWPlate_1_Position = new DWPlatePosition();
                                                DWPlate_1_Position.DWPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                                DWPlate_1_Position.TubeGroupID = NewTubeGroup.TubeGroupID;
                                                DWPlate_1_Position.Position = HolePosition + 1 + demandIndex;
                                                DWPlate_1_Position.PlateID = DWPlate_1.PlateID;
                                                DWPlate_1_Position.Tubes.Add(tube);
                                                _WanTaiEntities.AddToDWPlatePositions(DWPlate_1_Position);
                                                DWPlate_1_PositionList.Add(DWPlate_1_Position);
                                            }else
                                                DWPlate_1_PositionList[demandIndex].Tubes.Add(tube);
                                            DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate1 + "," + (HolePosition + demandIndex + 1).ToString());
                                        }
                                        else
                                        {
                                            if (DWPlate_2_PositionList.Count < 8)
                                            {
                                                DWPlate_2_Position = new DWPlatePosition();
                                                DWPlate_2_Position.DWPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                                DWPlate_2_Position.TubeGroupID = NewTubeGroup.TubeGroupID;
                                                DWPlate_2_Position.Position = HolePosition + 1 + demandIndex;
                                                DWPlate_2_Position.PlateID = DWPlate_2.PlateID;

                                                DWPlate_2_Position.Tubes.Add(tube);
                                                _WanTaiEntities.AddToDWPlatePositions(DWPlate_2_Position);
                                                DWPlate_2_PositionList.Add(DWPlate_2_Position);
                                            }
                                            else
                                                DWPlate_2_PositionList[demandIndex].Tubes.Add(tube);
                                            DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate2 + "," + (HolePosition + demandIndex + 1).ToString());
                                        }
                                        PoolingWorkListRowCount++;
                                        if (demand <= 8 && (tubeCount++) < 8)
                                        {
                                            //PCR文件
                                            foreach (TestingItemConfiguration TestionItem in TestingItemList)
                                            {
                                                if (_TubeGroup.TestingItemConfigurations.Where(_TestionItem => _TestionItem.TestingItemID == TestionItem.TestingItemID).Count() == 0)
                                                    continue;
                                                //if (!DelTubesBatch.TestingItem.ContainsKey(TestionItem.TestingItemID)) continue;
                                                //if (DelTubesBatch.TestingItem[TestionItem.TestingItemID] == 0) continue;
                                                PCRPlatePosition _PCRPlatePosition = new PCRPlatePosition();
                                                _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                                _PCRPlatePosition.PlateID = TestionItem.PlateID;// PCRPlate.PlateID;
                                                _PCRPlatePosition.TestName = TestionItem.TestingItemName;
                                                _PCRPlatePosition.Position = TestionItem.TestingItemPosition;
                                                _PCRPlatePosition.DWPlatePositions.Add(DWPlate_1_PositionList[tubeCount-1]);
                                                _PCRPlatePosition.DWPlatePositions.Add(DWPlate_2_PositionList[tubeCount-1]);
                                                //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                                                //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                                                _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);

                                                PCRCSV[TestionItem.TestingItemPCR - 1] = PlateName.DWPlate5 + "," + (HolePosition + demandIndex + 1).ToString() + ",20," + PlateName.PCRPlate + "," + TestionItem.TestingItemPosition.ToString();
                                                TestionItem.TestingItemPosition += 1;
                                                TestionItem.TestingItemPCR += 1;
                                            }
                                        }
                                      
                                        TubesPositionIndex++;
                                        /*参数下调*/
                                        BalanceTubesCount--;
                                        demand--;
                                        demandIndex ++ ;
                                        if (demandIndex == 8)
                                            demandIndex = 0;
                                        if (demand == 0)
                                        {
                                            DWPlate_1_PositionList = new List<DWPlatePosition>();
                                            DWPlate_2_PositionList = new List<DWPlatePosition>();
                                            tubeCount = 0;
                                            demand = PollingRules.TubeNumber * 8;
                                            HolePosition = HolePosition + 8 ;
                                        }
                                    }
                                    #endregion 
                                    int GroupNumber = BalanceTubesCount / PollingRules.TubeNumber;
                                    int GroupResidue = BalanceTubesCount % PollingRules.TubeNumber;
                                    int TubesIndex = 0;
                                    int CTubesIndex = 0;
                                    int Hole1TubesNumber = PollingRules.TubeNumber / 2;
                                    int Hole2TubesNumber = PollingRules.TubeNumber / 2;
                                    #region  原worklist生成规则
                                    // foreach (string str in _TubeGroup.TubesPosition.Split(']'))
                                    while (TubesPosition.Length > TubesPositionIndex)
                                    {
                                        string str = TubesPosition[TubesPositionIndex++];
                                        #region 保存tube信息
                                        if (string.IsNullOrEmpty(str)) continue;
                                        string TubePosition = str.Remove(0, 1);
                                        int ColumnIndex = int.Parse(TubePosition.Split(',')[0]);
                                        int RowIndex = int.Parse(TubePosition.Split(',')[1]) - 1;
                                        Tube tube = new Tube();
                                        tube.BarCode = Tubes.Rows[RowIndex]["BarCode" + ColumnIndex.ToString()].ToString();
                                        tube.ExperimentID = ExperimentID;
                                        tube.Position = RowIndex + 1;
                                        tube.Grid = ColumnIndex;
                                        tube.TubeGroupID = NewTubeGroup.TubeGroupID;
                                        tube.TubeID = WanTaiObjectService.NewSequentialGuid();
                                        tube.TubePosBarCode = Tubes.Rows[RowIndex]["TubePosBarCode" + ColumnIndex.ToString()].ToString();
                                        tube.TubeType = (int)Tubetype.Tube;
                                        tube.Volume = Volume;
                                        #endregion
                                        #region 开始先保存PCR
                                        if (CTubesIndex == 0)
                                        {
                                            DWPlate_1_Position = new DWPlatePosition();
                                            DWPlate_1_Position.DWPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                            DWPlate_1_Position.TubeGroupID = NewTubeGroup.TubeGroupID;
                                            DWPlate_1_Position.Position = HolePosition + 1;
                                            DWPlate_1_Position.PlateID = DWPlate_1.PlateID;

                                            DWPlate_2_Position = new DWPlatePosition();
                                            DWPlate_2_Position.DWPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                            DWPlate_2_Position.TubeGroupID = NewTubeGroup.TubeGroupID;
                                            DWPlate_2_Position.Position = HolePosition + 1;
                                            DWPlate_2_Position.PlateID = DWPlate_2.PlateID;

                                            //PCR文件
                                            foreach (TestingItemConfiguration TestionItem in TestingItemList)
                                            {
                                                if (_TubeGroup.TestingItemConfigurations.Where(_TestionItem => _TestionItem.TestingItemID == TestionItem.TestingItemID).Count() == 0)
                                                    continue;
                                                //if (!DelTubesBatch.TestingItem.ContainsKey(TestionItem.TestingItemID)) continue;
                                                //if (DelTubesBatch.TestingItem[TestionItem.TestingItemID] == 0) continue;
                                                PCRPlatePosition _PCRPlatePosition = new PCRPlatePosition();
                                                _PCRPlatePosition.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                                _PCRPlatePosition.PlateID = TestionItem.PlateID;// PCRPlate.PlateID;
                                                _PCRPlatePosition.TestName = TestionItem.TestingItemName;
                                                _PCRPlatePosition.Position = TestionItem.TestingItemPosition;
                                                _PCRPlatePosition.DWPlatePositions.Add(DWPlate_1_Position);
                                                _PCRPlatePosition.DWPlatePositions.Add(DWPlate_2_Position);
                                                //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_1.DWPlatePositionID).FirstOrDefault());
                                                //_PCRPlatePosition.DWPlatePositions.Add(_WanTaiEntities.DWPlatePositions.Where(d => d.DWPlatePositionID == DWPlate_NegativeControl_2.DWPlatePositionID).FirstOrDefault());
                                                _WanTaiEntities.AddToPCRPlatePositions(_PCRPlatePosition);

                                                PCRCSV[TestionItem.TestingItemPCR - 1] = PlateName.DWPlate5 + "," + (HolePosition + 1).ToString() + ",20," + PlateName.PCRPlate + "," + TestionItem.TestingItemPosition.ToString();
                                                TestionItem.TestingItemPosition += 1;
                                                TestionItem.TestingItemPCR += 1;
                                            }
                                            #region 废除的代码
                                            //foreach (TestingItemConfiguration _TestingItemConfiguration in _TubeGroup.TestingItemConfigurations)
                                            //{
                                            //    if (_TestingItemConfiguration.TestingItemName == "HBV")
                                            //    {
                                            //        PCRPositionHBV = new PCRPlatePosition();
                                            //        PCRPositionHBV.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                            //        PCRPositionHBV.PlateID = PCRPlate.PlateID;
                                            //        PCRPositionHBV.TestName = _TestingItemConfiguration.TestingItemName;
                                            //        PCRPositionHBV.Position = HBVPosition;
                                            //        PCRCSV[HBVPosition++] = PlateName.DWPlate5+"," + (HolePosition).ToString() + ",20,"+PlateName.PCRPlate+"," + HBVPosition;
                                            //    }
                                            //    if (_TestingItemConfiguration.TestingItemName == "HCV")
                                            //    {
                                            //        PCRPositionHCV = new PCRPlatePosition();
                                            //        PCRPositionHCV.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                            //        PCRPositionHCV.PlateID = PCRPlate.PlateID;
                                            //        PCRPositionHCV.TestName = _TestingItemConfiguration.TestingItemName;
                                            //        PCRPositionHCV.Position = HCVPosition;
                                            //        PCRCSV[HCVPosition++] = PlateName.DWPlate5+"," + (HolePosition).ToString() + ",20,"+PlateName.PCRPlate+"," + HCVPosition;
                                            //    }
                                            //    if (_TestingItemConfiguration.TestingItemName == "HIV")
                                            //    {
                                            //        PCRPositionHIV = new PCRPlatePosition();
                                            //        PCRPositionHIV.PCRPlatePositionID = WanTaiObjectService.NewSequentialGuid();
                                            //        PCRPositionHIV.PlateID = PCRPlate.PlateID;
                                            //        PCRPositionHIV.TestName = _TestingItemConfiguration.TestingItemName;
                                            //        PCRPositionHIV.Position = HIVPosition;
                                            //        PCRCSV[HIVPosition++] = PlateName.DWPlate5+"," + (HolePosition).ToString() + ",20,"+PlateName.PCRPlate+"," + HIVPosition;
                                            //    }
                                            //}
                                            #endregion
                                        }
                                        #endregion
                                        #region 没有剩余 或 还没到最后余数
                                        if (GroupResidue == 0 || GroupNumber > TubesIndex) ///无剩余，或没到最后
                                        {
                                            Volume = 960 / PollingRules.TubeNumber;
                                            Hole1TubesNumber = PollingRules.TubeNumber / 2;
                                            Hole2TubesNumber = PollingRules.TubeNumber / 2;
                                            CTubesIndex++;
                                            #region 单检
                                            if (PollingRules.TubeNumber == 1)
                                            {
                                                Volume = 480;
                                                DWPlate_1_Position.Tubes.Add(tube);
                                                DWPlate_2_Position.Tubes.Add(tube);
                                                //DWPlate_1_Position.Tubes.Add(_WanTaiEntities.Tubes.Where(t=>t.TubeID==tube.TubeID).FirstOrDefault());
                                                //DWPlate_2_Position.Tubes.Add(_WanTaiEntities.Tubes.Where(t => t.TubeID == tube.TubeID).FirstOrDefault());
                                                _WanTaiEntities.AddToDWPlatePositions(DWPlate_1_Position);
                                                _WanTaiEntities.AddToDWPlatePositions(DWPlate_2_Position);

                                                // _WanTaiEntities.SaveChanges();

                                                DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate1 + "," + (HolePosition + 1).ToString());
                                                DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate2 + "," + (HolePosition + 1).ToString());

                                                HolePosition++;
                                                CTubesIndex = 0;
                                                TubesIndex++;
                                                PoolingWorkListRowCount++;
                                                PoolingWorkListRowCount++;
                                            }
                                            #endregion
                                            else
                                            {
                                                if (CTubesIndex <= Hole1TubesNumber)
                                                {
                                                    DWPlate_1_Position.Tubes.Add(tube);
                                                    //DWPlate_1_Position.Tubes.Add(_WanTaiEntities.Tubes.Where(t=>t.TubeID==tube.TubeID).FirstOrDefault());
                                                    DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate1 + "," + (HolePosition + 1).ToString());
                                                    PoolingWorkListRowCount++;
                                                }
                                                else
                                                {
                                                    _WanTaiEntities.AddToDWPlatePositions(DWPlate_1_Position);
                                                    DWPlate_2_Position.Tubes.Add(tube);

                                                    DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate2 + "," + (HolePosition + 1).ToString());
                                                    PoolingWorkListRowCount++;
                                                }
                                                if (CTubesIndex == PollingRules.TubeNumber)
                                                {
                                                    _WanTaiEntities.AddToDWPlatePositions(DWPlate_2_Position);
                                                    CTubesIndex = 0;
                                                    HolePosition++;
                                                    TubesIndex++;
                                                }
                                                //_WanTaiEntities.SaveChanges();
                                            }
                                            // _WanTaiEntities.SaveChanges();
                                        }
                                        #endregion
                                        else ///最后一批
                                        {
                                            CTubesIndex++;
                                            #region  最后只有一个血管
                                            if (GroupResidue == 1)
                                            {
                                                Volume = 480;
                                                DWPlate_1_Position.Tubes.Add(tube);
                                                _WanTaiEntities.AddToDWPlatePositions(DWPlate_1_Position);
                                                DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate1 + "," + (HolePosition + 1).ToString());
                                                if (_TubeGroup.isComplement) //补液
                                                {
                                                    if (SystemFluid.Count == 0)
                                                    {
                                                        ErrMsg = "没有样品补充！";
                                                        ErrType = -1;

                                                        DWStreamWriter.Close();
                                                        //PCRStreamWriter.Close();
                                                        DWFile.Close();
                                                        DWFile.Dispose();
                                                        //PCRFile.Close();
                                                        //PCRFile.Dispose();
                                                        File.Delete(DWFileName);
                                                        //File.Delete(PCRFileName);
                                                        return DelTubesBatch;

                                                    }
                                                    Volume = 480;
                                                    while (SystemFluid[SystemFluidIndex].Volume < Volume)
                                                    {
                                                        SystemFluidIndex++;
                                                        if (SystemFluidIndex >= SystemFluid.Count)
                                                        {
                                                            ErrType = -1;
                                                            ErrMsg = "样品补充液不够！";
                                                            DWStreamWriter.Close();
                                                            //PCRStreamWriter.Close();
                                                            DWFile.Close();
                                                            DWFile.Dispose();
                                                            //PCRFile.Close();
                                                            //PCRFile.Dispose();
                                                            File.Delete(DWFileName);
                                                            //File.Delete(PCRFileName);
                                                            return DelTubesBatch;
                                                        }
                                                    }
                                                    DWStreamWriter.WriteLine("Tube" + SystemFluid[SystemFluidIndex].Grid.ToString() + "," + SystemFluid[SystemFluidIndex].Position.ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate2 + "," + (HolePosition + 1).ToString());
                                                    SystemFluid[SystemFluidIndex].Volume -= Volume;
                                                    PoolingWorkListRowCount++;
                                                }
                                                else
                                                {
                                                    DWPlate_2_Position.Tubes.Add(tube);
                                                    //DWPlate_1_Position.Tubes.Add(_WanTaiEntities.Tubes.Where(t=>t.TubeID==tube.TubeID).FirstOrDefault());
                                                    //DWPlate_2_Position.Tubes.Add(_WanTaiEntities.Tubes.Where(t => t.TubeID == tube.TubeID).FirstOrDefault());
                                                    _WanTaiEntities.AddToDWPlatePositions(DWPlate_2_Position);
                                                    DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate2 + "," + (HolePosition + 1).ToString());
                                                }
                                                HolePosition++;
                                                CTubesIndex = 0;
                                                TubesIndex++;
                                                PoolingWorkListRowCount++;
                                                PoolingWorkListRowCount++;
                                            }
                                            #endregion
                                            #region 最后大于一个血管
                                            else
                                            {
                                                if (GroupResidue <= (PollingRules.TubeNumber / 2))
                                                {
                                                    Hole1TubesNumber = GroupResidue / 2;
                                                    if (GroupResidue % 2 > 0)
                                                        Hole1TubesNumber++;
                                                    Hole2TubesNumber = GroupResidue - Hole1TubesNumber;
                                                }
                                                else
                                                {
                                                    Hole1TubesNumber = PollingRules.TubeNumber / 2;
                                                    Hole2TubesNumber = GroupResidue - Hole1TubesNumber;
                                                }
                                                if (_TubeGroup.isComplement) //补液
                                                {
                                                    if (SystemFluid.Count == 0)
                                                    {
                                                        ErrMsg = "没有样品补充！";
                                                        ErrType = -1;
                                                        DWStreamWriter.Close();
                                                        //PCRStreamWriter.Close();
                                                        DWFile.Close();
                                                        DWFile.Dispose();
                                                        //PCRFile.Close();
                                                        //PCRFile.Dispose();
                                                        File.Delete(DWFileName);
                                                        //File.Delete(PCRFileName);
                                                        return DelTubesBatch;
                                                    }
                                                    #region 补液
                                                    if (CTubesIndex <= Hole1TubesNumber)
                                                    {
                                                        DWPlate_1_Position.Tubes.Add(tube);
                                                        //DWPlate_1_Position.Tubes.Add(_WanTaiEntities.Tubes.Where(t=>t.TubeID==tube.TubeID).FirstOrDefault());
                                                        Volume = 960 / PollingRules.TubeNumber;
                                                        DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate1 + "," + (HolePosition + 1).ToString());
                                                        PoolingWorkListRowCount++;
                                                    }
                                                    else
                                                    {
                                                        _WanTaiEntities.AddToDWPlatePositions(DWPlate_1_Position);
                                                        DWPlate_2_Position.Tubes.Add(tube);
                                                        // DWPlate_2_Position.Tubes.Add(_WanTaiEntities.Tubes.Where(t => t.TubeID == tube.TubeID).FirstOrDefault());

                                                        Volume = 960 / PollingRules.TubeNumber;
                                                        DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate2 + "," + (HolePosition + 1).ToString());
                                                        PoolingWorkListRowCount++;
                                                    }
                                                    //  _WanTaiEntities.SaveChanges();
                                                    #region 补液
                                                    //第一块板需要补液
                                                    if (CTubesIndex == Hole1TubesNumber && Hole1TubesNumber != PollingRules.TubeNumber / 2)
                                                    {
                                                        Volume = (960 / PollingRules.TubeNumber) * (PollingRules.TubeNumber / 2 - Hole1TubesNumber);
                                                        while (SystemFluid[SystemFluidIndex].Volume < Volume)
                                                        {
                                                            SystemFluidIndex++;
                                                            if (SystemFluidIndex >= SystemFluid.Count)
                                                            {
                                                                ErrType = -1;
                                                                ErrMsg = "样品补充液不够！";
                                                                DWStreamWriter.Close();
                                                                // PCRStreamWriter.Close();
                                                                DWFile.Close();
                                                                DWFile.Dispose();
                                                                //PCRFile.Close();
                                                                //PCRFile.Dispose();
                                                                File.Delete(DWFileName);
                                                                // File.Delete(PCRFileName);
                                                                return DelTubesBatch;
                                                            }
                                                        }
                                                        DWStreamWriter.WriteLine("Tube" + SystemFluid[SystemFluidIndex].Grid.ToString() + "," + SystemFluid[SystemFluidIndex].Position.ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate1 + "," + (HolePosition + 1).ToString());
                                                        SystemFluid[SystemFluidIndex].Volume -= Volume;
                                                        PoolingWorkListRowCount++;
                                                    }
                                                    //第二块板需要补液
                                                    if (CTubesIndex == (Hole1TubesNumber + Hole2TubesNumber) && Hole2TubesNumber != PollingRules.TubeNumber / 2)
                                                    {
                                                        Volume = (960 / PollingRules.TubeNumber) * (PollingRules.TubeNumber / 2 - Hole2TubesNumber);
                                                        while (SystemFluid[SystemFluidIndex].Volume < Volume)
                                                        {
                                                            SystemFluidIndex++;
                                                            if (SystemFluidIndex >= SystemFluid.Count)
                                                            {
                                                                ErrType = -1;
                                                                ErrMsg = "样品补充液不够！";
                                                                DWStreamWriter.Close();
                                                                //PCRStreamWriter.Close();
                                                                DWFile.Close();
                                                                DWFile.Dispose();
                                                                // PCRFile.Close();
                                                                //PCRFile.Dispose();
                                                                File.Delete(DWFileName);
                                                                // File.Delete(PCRFileName);
                                                                return DelTubesBatch;
                                                            }
                                                        }
                                                        DWStreamWriter.WriteLine("Tube" + SystemFluid[SystemFluidIndex].Grid.ToString() + "," + SystemFluid[SystemFluidIndex].Position.ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate2 + "," + (HolePosition + 1).ToString());
                                                        PoolingWorkListRowCount++;
                                                        SystemFluid[SystemFluidIndex].Volume -= Volume;

                                                    }
                                                    if (CTubesIndex == (Hole1TubesNumber + Hole2TubesNumber))
                                                    {
                                                        _WanTaiEntities.AddToDWPlatePositions(DWPlate_2_Position);
                                                        CTubesIndex = 0;
                                                        HolePosition++;
                                                        TubesIndex++;
                                                    }
                                                    #endregion 补液结束
                                                    #endregion 补液结束
                                                }
                                                else
                                                {
                                                    #region 不补液
                                                    if (CTubesIndex <= Hole1TubesNumber)
                                                    {
                                                        // DWPlate_1_Position.Tubes.Add(_WanTaiEntities.Tubes.Where(t=>t.TubeID==tube.TubeID).FirstOrDefault());
                                                        DWPlate_1_Position.Tubes.Add(tube);
                                                        Volume = (int)(480 / Hole1TubesNumber);
                                                        if (Volume > 480) Volume = 480;
                                                        DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate1 + "," + (HolePosition + 1).ToString());
                                                        PoolingWorkListRowCount++;
                                                    }
                                                    else
                                                    {
                                                        _WanTaiEntities.AddToDWPlatePositions(DWPlate_1_Position);
                                                        DWPlate_2_Position.Tubes.Add(tube);
                                                        Volume = (int)(480 / Hole2TubesNumber);
                                                        if (Volume > 480) Volume = 480;
                                                        DWStreamWriter.WriteLine("Tube" + tube.Grid + "," + (RowIndex + 1).ToString() + "," + Volume.ToString() + "," + PlateName.DWPlate2 + "," + (HolePosition + 1).ToString());
                                                        PoolingWorkListRowCount++;
                                                    }

                                                    #region 最后
                                                    if (CTubesIndex == (Hole1TubesNumber + Hole2TubesNumber))
                                                    {
                                                        _WanTaiEntities.AddToDWPlatePositions(DWPlate_2_Position);

                                                        CTubesIndex = 0;
                                                        HolePosition++;
                                                        TubesIndex++;
                                                    }
                                                    #endregion

                                                    #endregion 不补液结束
                                                }
                                            }
                                            #endregion
                                        }
                                        tube.Volume = Volume;
                                        //_WanTaiEntities.SaveChanges();
                                        // _WanTaiEntities.Tubes.Where(t=>t.TubeID==tube.TubeID).f;
                                        // NewTubeGroup.Tubes.Add(tube);
                                    }
                                    #endregion
                                }
                                //NewTubeGroup.EntityKey = new EntityKey() { EntityContainerName = "TubeGroups",
                                //                                           EntitySetName = "TubeGroups",
                                //                                           EntityKeyValues = new EntityKeyMember[] { new EntityKeyMember() { Key = "TubeGroups", Value = NewTubeGroup.TubeGroupID } }
                                //};
                            }
                            //foreach (String str in PCRCSV)
                            //    PCRStreamWriter.WriteLine(str);
                            DWStreamWriter.Close();
                           // PCRStreamWriter.Close();

                            PoolCountInTotal = HolePosition;
                            formulaParameters.PoolCountInTotal = PoolCountInTotal;
                            formulaParameters.PoolingWorkListRowCount = PoolingWorkListRowCount++;
                            if (!SessionInfo.RotationFormulaParameters.ContainsKey(Guid.Empty))
                            {
                                SessionInfo.RotationFormulaParameters.Add(Guid.Empty, formulaParameters);
                            }
                            else 
                            {
                                SessionInfo.RotationFormulaParameters[Guid.Empty] = formulaParameters;
                            }

                            string SampleNumberFileName = WanTai.Common.Configuration.GetEvoVariableOutputPath()+(ExperimentRotation == null ? "" : ExperimentRotation.RotationID.ToString()) + WanTai.Common.Configuration.GetSampleNumberFileName();
                            using (StreamWriter writer = new StreamWriter(new FileStream(SampleNumberFileName, FileMode.Create, FileAccess.Write)))
                            {
                                writer.WriteLine("SAMPLE_NUM_EX");
                                writer.WriteLine(PoolCountInTotal);
                            }
                            string MixSampleNumberFileName = WanTai.Common.Configuration.GetEvoVariableOutputPath() + (ExperimentRotation == null ? "" : ExperimentRotation.RotationID.ToString()) + WanTai.Common.Configuration.GetMixSampleNumberFileName();
                            using (StreamWriter writer = new StreamWriter(new FileStream(MixSampleNumberFileName, FileMode.Create, FileAccess.Write)))
                            {
                                int index = 0;
                                foreach (TestingItemConfiguration TestionItem in TestingItemList)
                                {
                                    //if (File.Exists(CSVPath + TestionItem.WorkListFileName))
                                    //    File.Delete(CSVPath + TestionItem.WorkListFileName);
                                    int testItemCount = 0;
                                    //if (!DelTubesBatch.TestingItem.ContainsKey(TestionItem.TestingItemID)) continue;
                                    //if (DelTubesBatch.TestingItem[TestionItem.TestingItemID] == 0) continue;
                                    if (DelTubesBatch.TestingItem.ContainsKey(TestionItem.TestingItemID))
                                        testItemCount=(DelTubesBatch.TestingItem[TestionItem.TestingItemID] + 2);
                                    writer.WriteLine("MIXSAMPLE_" + TestionItem.TestingItemName + "_NUM" + "," + testItemCount.ToString());
                                    if (!DelTubesBatch.TestingItem.ContainsKey(TestionItem.TestingItemID)) continue;
                                    if (DelTubesBatch.TestingItem[TestionItem.TestingItemID] == 0) continue;
                                    if (string.IsNullOrEmpty(TestionItem.WorkListFileName)) continue;

                                    using (StreamWriter PCR = new StreamWriter(new FileStream(CSVPath + (ExperimentRotation == null ? "" : ExperimentRotation.RotationID.ToString()) + TestionItem.WorkListFileName, FileMode.Create, FileAccess.Write)))
                                    {
                                        PCR.WriteLine("Source Labware Label,Source Position,Volume,Destination Labware Label,Destination Position");
                                        for (int TestionCount=0; TestionCount < DelTubesBatch.TestingItem[TestionItem.TestingItemID]+2; TestionCount++, index++)
                                            PCR.WriteLine(PCRCSV[index]);
                                        PCR.Close();
                                    }
                                }
                                writer.Close();
                            }
                        }
                    }
                    #endregion 开始保存文件
                    _WanTaiEntities.SaveChanges();

                    
                  //  DataModel.FormulaParameters.PCRWorkListRowCount = DelTubesBatch.HBVNumber + (DelTubesBatch.HBVNumber > 0 ? 2 : 0) + DelTubesBatch.HCVNumber + (DelTubesBatch.HCVNumber > 0 ? 2 : 0) + DelTubesBatch.HIVNumber + (DelTubesBatch.HIVNumber > 0 ? 2 : 0);
                    return _TubesBatch;
                }
            }
            catch (Exception e)
            {
                string errorMessage = e.Message + System.Environment.NewLine + e.StackTrace;
                LogInfoController.AddLogInfo(LogInfoLevelEnum.Error, errorMessage, SessionInfo.LoginName, this.GetType().ToString() + "->" + "SaveTubesGroup()", SessionInfo.ExperimentID);
                throw;
            }
        }
    }
}