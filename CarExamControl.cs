﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Practices.ServiceLocation;
using Model;
using System.IO;
using GalaSoft.MvvmLight.Messaging;
using Newtonsoft.Json;
using DevExpress.XtraEditors;
using Service;
using System.Configuration;

namespace DXA_HSJX
{
    [ToolboxItem(true)]
    public partial class CarExamControl : UserControl
    {

        public int Position = 0;
     
        public string IDCard = string.Empty;
        public string LicensePlate = string.Empty;

        private string ReportCardPath = string.Empty;
        public IMessenger Messenger;

        public IExamService examService;
        //当前考生临时变量 用来保存考生信息
        private ExamCar CurrentExamCar;
        //System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(179)))), ((int)(((byte)(45)))));
        Color OriginalColor = Color.FromArgb(112, 179, 45);
        Color BgColor = Color.FromArgb(56, 71, 102);
        Color FinishColor = Color.Red;
        Color DisableColor = Color.FromArgb(96, 96, 96);
        Color EnterExamColor = Color.Green;
        Color LeaveExamColor = Color.Red;

        private UdpServer udpServer;


        private List<DeductionRule> _deductionRules = new List<DeductionRule>();
        BindingSource ruleBindingSource;
        public CarExamControl()
        {
            InitializeComponent();
            dgvDeductRules.AutoGenerateColumns = false;
            ReportCardPath = ConfigurationManager.AppSettings["ReportCardPath"];
        }
        public void InitInstance(IMessenger messager,UdpServer server,IExamService examService)
        {
            this.Messenger = messager;
            this.udpServer = server;
            this.examService = examService;
        }
        public Image BytesToImage(byte[] buffer)
        {
            MemoryStream ms = new MemoryStream(buffer);
            Image image = System.Drawing.Image.FromStream(ms);
            return image;
        }
        /// <summary>
        /// 接收到考试车辆发送改变消息
        /// </summary>
        /// <param name="message"></param>
        public void updateExamCarInfo(ExamCarChangeMessage message)
        {

            CurrentExamCar = message.ExamCar;
            var examStudent = message.ExamCar.ExamStudent;
            lblNameStatusScore.Text = string.Format("姓名:{0}   状态:初考   成绩：100分", examStudent.Name);
            lblIdCard.Text = string.Format("身份证号码：{0}", examStudent.IdCard);
            lblLicensePlateUseTime.Text = string.Format("车牌：{0}  用时：00:00:00 ", message.ExamCar.LicensePlate);
            lblSensor.Text = "车速:0 Km/h 转速:0Rpm 卫星:0 精度:0 纬度:0 里程:0米";
            IDCard = examStudent.IdCard;
            LicensePlate = message.ExamCar.LicensePlate;

            if (examStudent.IDCardImage!=null)
            {
                var Image = BytesToImage(examStudent.IDCardImage);
                PictureBoxIDCard.Image = Image;
            }

            //更新头像

            Init();
        }
        /// <summary>
        /// 接收到考试信息
        /// </summary>
        /// <param name="message"></param>
        public  void updateExamMessage(ExamMessage message)
        {
          
           this.InvokeAsync(() =>
                {
                    switch (message.CmdType)
                    {
                        case CmdTypeEnum.StartExam:
                            StartExam(message);
                            break;
                        case CmdTypeEnum.EndExam:
                            EndExam(message);
                            break;
                        case CmdTypeEnum.BreakeRule:
                            BreakeRule(message);
                            break;
                        case CmdTypeEnum.Capture:
                            break;
                        case CmdTypeEnum.EnterExamItem:
                            EnterExamItem(message);
                            break;
                        case CmdTypeEnum.LeaveExamItem:
                            LeaveExamItem(message);
                            break;
                        case CmdTypeEnum.SendExamStudentInfo:
                            break;
                        case CmdTypeEnum.SendCarSensor:
                            SendCarSensor(message);
                            break;
                        default:
                            break;
                    }
                });
            Task.Run(() => {
                examService.AddExamInfo(message);
            });


           
                //表明是当前考生
        }



       /// <summary>
       /// 如果是通过项目
       /// </summary>
       /// <param name="cmdType"></param>
        public void SendMessage(CmdTypeEnum cmdType,ExamItemEnum ExamItem=ExamItemEnum.CommonExamItem,DeductionRule rule=null)
        {
            if (CurrentExamCar==null)
            {
                MessageBox.Show("请分配考生", "监考大厅");
                return;
            }
            ExamMessage message = new ExamMessage();
            message.ExamStudent =CurrentExamCar.ExamStudent;
       
            message.SendTime = DateTime.Now;
            message.CmdType = cmdType;
            message.ExamItem = ExamItem;

            if (message.CmdType==CmdTypeEnum.BreakeRule)
            {
                //发送扣分
                message.DeductionRule = new ExamMessageDeductionRule();
                message.DeductionRule.ExamItemId = rule.ExamItemId;
                message.DeductionRule.RuleCode = rule.RuleCode;
                message.DeductionRule.RuleName = rule.RuleName;
                message.DeductionRule.ExamItemName = rule.ExamItemName;
                message.DeductionRule.SubRuleCode = rule.SubRuleCode;
                message.DeductionRule.DeductedScores = rule.DeductedScores;
            }
           // message.DeductionRule = rule;

            var msg=  JsonConvert.SerializeObject(message);
            //消息以Upd的行驶发出去
            udpServer.Send(msg, CurrentExamCar.Ip, CurrentExamCar.Port);
        }


        public void BtnDisable(SimpleButton btn)
        {
            btn.Enabled = false;
            btn.Appearance.BackColor = DisableColor;
        }
        public void BtnEnable(SimpleButton btn)
        {
            btn.Enabled = true;
            btn.Appearance.BackColor= OriginalColor;
        }
        public void StartExam(ExamMessage message)
        {
            var startExamTime = message.SendTime;
            lblStartTime.Text = string.Format("开始考试:{0:00}:{1:00}:{2:00}", startExamTime.Hour, startExamTime.Minute, startExamTime.Second);
            var status = message.IsPreliminaryExam ? "初考" : "补考";
            lblNameStatusScore.Text = string.Format("姓名:{0}   状态:{1}   成绩：{2}分",message.ExamStudent.Name, status,message.Score);
            Init();

            BtnDisable(btnStartExam);
            BtnDisable(btnPrint);
            BtnEnable(btnArtificialEvaluation);
            BtnDisable(btnSendExam);
            BtnEnable(btnEndExam);
        }
        public void EndExam(ExamMessage message)
        {
            var endExamTime = message.SendTime;
            lblEndTime.Text = string.Format("考试结束:{0:00}:{1:00}:{2:00}", endExamTime.Hour, endExamTime.Minute, endExamTime.Second);

            BtnEnable(btnStartExam);
            BtnEnable(btnPrint);
            BtnDisable(btnArtificialEvaluation);
            BtnEnable(btnSendExam);
            BtnDisable(btnEndExam);
        
            btnEndExam.Enabled = false;
            btnSendExam.Enabled = true;
            btnPrint.Enabled = true;
            btnArtificialEvaluation.Enabled = false;
            btnSendExam.Enabled = true;

        }
        public void BreakeRule(ExamMessage message)
        {
            var _deductionRule = new DeductionRule();
            _deductionRule.ExamItemName = message.DeductionRule.ExamItemName;
            _deductionRule.RuleName = message.DeductionRule.DeductedReason;
            _deductionRule.DeductedScores = message.DeductionRule.DeductedScores;

            //扣分还要更新分数
            _deductionRules.Insert(0, _deductionRule);
            ruleBindingSource = new BindingSource(_deductionRules, "");
            dgvDeductRules.DataSource = ruleBindingSource;
            dgvDeductRules.Refresh();
            

            var status = message.IsPreliminaryExam ? "初考" : "补考";
            lblNameStatusScore.Text = string.Format("姓名:{0}   状态:{1}   成绩：{2}分", message.ExamStudent.Name, status, message.Score);

        }
        public void EnterExamItem(ExamMessage message)
        {
            switch (message.ExamItem)
            {
                case ExamItemEnum.CommonExamItem:
                    break;
                case ExamItemEnum.ReverseParking:
                    btnReverseParking.Appearance.BackColor = EnterExamColor;
                    break;
                case ExamItemEnum.ReverseParkingEnd:
                    btnReverseParking.Appearance.BackColor = LeaveExamColor;
                    break;
                case ExamItemEnum.ParallelParking:
                    btnParallelParking.Appearance.BackColor = EnterExamColor;
                    break;
                case ExamItemEnum.ParallelParkingEnd:
                    btnParallelParking.Appearance.BackColor = LeaveExamColor;
                    break;
                case ExamItemEnum.SlopeWayParkingAndStarting:
                    btnSlopeWayParkingAndStarting.Appearance.BackColor = EnterExamColor;
                    break;
                case ExamItemEnum.SlopeWayParkingAndStartingEnd:
                    btnSlopeWayParkingAndStarting.Appearance.BackColor = LeaveExamColor;
                    break;
                case ExamItemEnum.QuarterTurning:
                    btnQuarterTurning.Appearance.BackColor = EnterExamColor;
                    break;
                case ExamItemEnum.QuarterTurningEnd:
                    btnQuarterTurning.Appearance.BackColor = LeaveExamColor;
                    break;
                case ExamItemEnum.CurveDriving:
                    //如果这个方案不行那就只有用WebApi上传照片了
                    btnCurveDriving.Appearance.BackColor = EnterExamColor;
                    break;
                case ExamItemEnum.CurveDrivingEnd:
                    btnCurveDriving.Appearance.BackColor = LeaveExamColor;
                    break;
                default:
                    break;
            }
        }
        public void LeaveExamItem(ExamMessage message)
        {
            EnterExamItem(message);
        }
        /// <summary>
        /// 更新信号状态
        /// </summary>
        /// <param name="message"></param>
        public void SendCarSensor(ExamMessage message)
        {


            var sensor = message.CarSignalInfo;
            if (sensor == null || message.CarSignalInfo.Sensor == null)
                return;
            lblSensor.Text = GetCarSensorStr(message.CarSignalInfo);
            lblLicensePlateUseTime.Text = GetLicensePlateUseTime(message.CarSignalInfo);

        }

        private string GetLicensePlateUseTime(CarSignalInfo carSignal)
        {
            var useTime = string.Format("{0:00}:{1:00}:{2:00}", carSignal.UsedTime.Hours, carSignal.UsedTime.Minutes, carSignal.UsedTime.Seconds);
            return string.Format("车牌：{0}  用时：{1} ", LicensePlate, useTime);
        }
        private string GetCarSensorStr(CarSignalInfo carSignal)
        {
            var gps = carSignal.Gps;
            var sensor = carSignal.Sensor;
            var count = string.Format("{0}/{1}", gps.TrackedSatelliteCount, gps.FixedSatelliteCount);
            var lat = string.Format("{0:#0.00000}°", gps.LatitudeDegrees);
            var lon = string.Format("{0:#0.00000}°", gps.LongitudeDegrees);
            var Distance = string.Format("{0:0}米", carSignal.Distance);
            string str = "车速:0 Km/h 转速:0Rpm,卫星:08 精度:0，纬度:0 里程:0米";

            str = string.Format("车速:{0} Km/h 转速:{1}Rpm,卫星:{2} 精度:{3}，纬度:{4} 里程:{5}", sensor.SpeedInKmh, sensor.EngineRpm, count, lat, lon,Distance);

            return str;
        }

        
        public void Init()
        {
            BtnEnable(btnStartExam);
            BtnDisable(btnPrint);
            BtnEnable(btnArtificialEvaluation);
            //todo:测试需要
           // BtnDisable(btnArtificialEvaluation);
            BtnEnable(btnSendExam);
            BtnDisable(btnEndExam);

            btnSlopeWayParkingAndStarting.Appearance.BackColor = BgColor;
            btnParallelParking.Appearance.BackColor = BgColor;
            btnQuarterTurning.Appearance.BackColor = BgColor;
            btnReverseParking.Appearance.BackColor = BgColor;
            btnCurveDriving.Appearance.BackColor = BgColor;
            btnSlopeWayParkingAndStarting.Appearance.BackColor = BgColor;
            dgvDeductRules.DataSource = null;
           
            //DataGridViewBreakeRule.cle

            //所有项目按钮恢复到最初
        }




        private void CarExamControl_Load(object sender, EventArgs e)
        {
            Init();
            //Messenger = Resolve<IMessenger>();
        }

        /// <summary>
        /// 打印按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPrint_Click(object sender, EventArgs e)
        {
            //打印需要获取数据首先生成word 然后调用打印机进行打印
            //需要身份证号码
            Task.Run(() => {
                BtnDisable(btnPrint);
                var model = examService.GetPrintScoreModel(IDCard);
                string wordPath = GenerateWord(model);
                printWord(wordPath);
                BtnEnable(btnPrint);
            });

         
           //第一步获取数据源 第二不进行打印
        }

        public string  GenerateWord(PrintScoreModel model)
        {
            WriteIntoWord wiw = null;
            wiw = new WriteIntoWord();
            var path = System.IO.Directory.GetCurrentDirectory() + "\\" + "PrintTemplate.dotx";
            wiw.CreateNewDocument(path);


            wiw.WriteIntoDocument(TemplateBookMarkName.name, model.Name);
            wiw.WriteIntoDocument(TemplateBookMarkName.IdCard, model.IDCard);
            wiw.WriteIntoDocument(TemplateBookMarkName.businessType, model.BusinessType);
            wiw.WriteIntoDocument(TemplateBookMarkName.examDate, model.ExamDate);
            wiw.WriteIntoDocument(TemplateBookMarkName.examAddress, "华山驾校十区一号线");
            wiw.WriteIntoDocument(TemplateBookMarkName.firstExamTime, model.FirstExam.ExamTime);
            wiw.WriteIntoDocument(TemplateBookMarkName.firstExamBreakeRule, model.FirstExam.DedictionRules);
            wiw.WriteIntoDocument(TemplateBookMarkName.firstExamScore, model.FirstExam.Score.ToString());

            wiw.WritePicIntoDocument(TemplateBookMarkName.IDCardImage, model.IDCardPath);
            wiw.WritePicIntoDocument(TemplateBookMarkName.firstExamFirstCapturePhoto, model.FirstExam.CaptureImageFirstPath);
            wiw.WritePicIntoDocument(TemplateBookMarkName.firstExamSecondCapturePhoto, model.FirstExam.CaptureImageSecondPath);
            wiw.WritePicIntoDocument(TemplateBookMarkName.firstExamThirdCapturePhoto, model.FirstExam.CaptureImageThirdPath);

            if (model.SecondExam!=null)
            {
                wiw.WriteIntoDocument(TemplateBookMarkName.SecondExamTime, model.SecondExam.ExamTime);
                wiw.WriteIntoDocument(TemplateBookMarkName.SecondExamBreakeRule, model.SecondExam.DedictionRules);
                wiw.WriteIntoDocument(TemplateBookMarkName.SecondExamScore, model.SecondExam.Score.ToString());
                wiw.WritePicIntoDocument(TemplateBookMarkName.SecondExamFirstCapturePhoto, model.SecondExam.CaptureImageFirstPath);
                wiw.WritePicIntoDocument(TemplateBookMarkName.SecondExamSecondCapturePhoto, model.SecondExam.CaptureImageSecondPath);
                wiw.WritePicIntoDocument(TemplateBookMarkName.SecondExamThirdCapturePhoto, model.SecondExam.CaptureImageThirdPath);
            }
           
            //图片写入指定路径
            path = ReportCardPath + "\\" +model.IDCard+"_"+DateTime.Now.ToString("yyyyMMddHHmmss") + ".doc";
            wiw.Save_CloseDocument(path);
            return path;
        }

        public void printWord(string path)
        {
            Spire.Doc.Document doc = new Spire.Doc.Document ();
            doc.LoadFromFile(path);

            PrintDialog dialog = new PrintDialog();
            dialog.AllowPrintToFile = true;
            dialog.AllowCurrentPage = true;
            dialog.AllowSomePages = true;
            dialog.UseEXDialog = true;
            doc.PrintDialog = dialog;

            System.Drawing.Printing.PrintDocument printDoc = doc.PrintDocument;
            printDoc.Print();
        }

        /// <summary>
        /// 人工评判
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnArtificialEvaluation_Click(object sender, EventArgs e)
        {
            ///如果存再性能就简单一点 不重复new 实例
            FormManualJudge form = new FormManualJudge();

            if (form.ShowDialog()==DialogResult.OK)
            {
                var rule = form.SelectedRule;
                //需要一个ExamItemName
                //表示有选中扣分规则
                if (rule!=null)
                {
                    SendMessage(CmdTypeEnum.BreakeRule, (ExamItemEnum)rule.ExamItemId, rule);
                }
            }
    
        }
        /// <summary>
        /// 发送考生
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSendExam_Click(object sender, EventArgs e)
        {
            SendMessage(CmdTypeEnum.SendExamStudentInfo);
        }
        /// <summary>
        /// 开始考试
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStartExam_Click(object sender, EventArgs e)
        {
            SendMessage(CmdTypeEnum.StartExam);
        }
        //结束考试
        private void btnEndExam_Click(object sender, EventArgs e)
        {
            SendMessage(CmdTypeEnum.EndExam);
        }

        private void btnExamItem_Click(object sender, EventArgs e)
        {
            var btn = (SimpleButton)sender;
            ExamItemEnum ExamItem = ExamItemEnum.CommonExamItem;

            if (btn.Text=="倒车入库")
            {
                ExamItem = ExamItemEnum.ReverseParking;
            }
            else if (btn.Text=="侧方停车")
            {
                ExamItem = ExamItemEnum.ParallelParking;
            }
            else if (btn.Text=="曲线行驶")
            {
                ExamItem = ExamItemEnum.QuarterTurning;
            }
            else if (btn.Text=="直角转弯")
            {
                ExamItem = ExamItemEnum.CurveDriving;
            }
            else if (btn.Text=="坡道起步")
            {
                ExamItem = ExamItemEnum.SlopeWayParkingAndStarting;
            }
            SendMessage(CmdTypeEnum.EnterExamItem, ExamItem);
        }
    }
}
