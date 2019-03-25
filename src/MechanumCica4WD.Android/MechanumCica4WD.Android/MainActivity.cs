using System;
using System.Threading;
using Android.App;
using Android.Content.PM;
using Android.Widget;
using Android.OS;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using LibBcore;
using MechanumCica4WD.Android.Models;
using MechanumCica4WD.Android.Views.Controls;
using Thread = Java.Lang.Thread;

namespace MechanumCica4WD.Android
{
    [Activity(Label = "MechanumCica4WD.Android", MainLauncher = true, ScreenOrientation = ScreenOrientation.Landscape)]
    public class MainActivity : AppCompatActivity
    {
        #region field

        private StickControllerView _stickController;

        private CicaBcoreManager _bcoreManager;

        private TextView _textStatus;
        private TextView _textFrontBattery;
        private TextView _textRearBattery;

        private DateTime _lasttimeSendData;

        private Handler _handlerControl;
        private bool _isUpdate;
        private float _fb;
        private float _lr;
        private float _ro;

        #endregion

        #region property

        #endregion

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetTheme(Resource.Style.MechanumCica4WD);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            SetFullScreen();

            InitBcore();

            InitView();
        }

        protected override void OnStart()
        {
            base.OnStart();

            _bcoreManager.Connect();
        }

        protected override void OnResume()
        {
            base.OnResume();

            Log.Debug("Cica4WD", "OnResume/hoge");
        }

        protected override void OnStop()
        {
            base.OnStop();

        }

        public override bool OnGenericMotionEvent(MotionEvent e)
        {
            //Log.Debug("Cica4W",
            //    $"lx:{e.GetAxisValue(Axis.X):f2}/ly:{e.GetAxisValue(Axis.Y):f2}");

            var rawX = e.GetAxisValue(Axis.X);
            var rawY = e.GetAxisValue(Axis.Y);
            var rotate = e.GetAxisValue(Axis.Z);

            var angle = Math.Atan2(rawY, rawX);
            var size = Math.Sqrt(rawX * rawX + rawY * rawY);

            if (size > 1.0) size = 1.0;

            var fb = (float)(size * Math.Sin(angle));
            var lr = (float)(size * Math.Cos(angle));

            if (Math.Abs(fb) < 0.1) fb = 0;
            if (Math.Abs(lr) < 0.1) lr = 0;
            if (Math.Abs(rotate) < 0.1) rotate = 0;

            var isForce = Math.Abs(fb) < 0.1 && Math.Abs(lr) < 0.1 && Math.Abs(rotate) < 0.1;
            var now = DateTime.Now;

            _fb = fb;
            _lr = lr;
            _ro = rotate;
            _isUpdate = true;
            //Log.Debug("Cica4WD", $"fb:{fb:f3}/lr:{lr:f3}/rotate:{rotate:f3}");

            _stickController?.SetStickRate(lr, fb, rotate);
            _lasttimeSendData = now;
            return base.OnGenericMotionEvent(e);
        }

        private void SetFullScreen()
        {
            var view = Window.DecorView;

            var current = (int) view.SystemUiVisibility;

            var newoption = current | (int) SystemUiFlags.ImmersiveSticky | (int) SystemUiFlags.Fullscreen |
                            (int) SystemUiFlags.HideNavigation;

            view.SystemUiVisibility = (StatusBarVisibility) newoption;
        }

        private void InitView()
        {
            _stickController = FindViewById<StickControllerView>(Resource.Id.stick_controller);

            _textStatus = FindViewById<TextView>(Resource.Id.text_status);
            _textFrontBattery = FindViewById<TextView>(Resource.Id.text_front_battery);
            _textRearBattery = FindViewById<TextView>(Resource.Id.text_rear_battery);
        }

        private void InitBcore()
        {
            _handlerControl = new Handler();
            _bcoreManager = new CicaBcoreManager(this);
            _bcoreManager.ConnectionStatusChanged += OnChangedConnectionStatus;
            _bcoreManager.BatteryVoltageRead += OnReadBcoreBattery;
        }

        private void OnReadBcoreBattery(object sender, ReadBatteryEventArgs e)
        {
            RunOnUiThread(() =>
            {
                if (e.Type == EBcoreType.Front)
                {
                    _textFrontBattery.Text = $"{e.Voltage,4}";
                }
                else
                {
                    _textRearBattery.Text = $"{e.Voltage,4}";
                }
            });
        }

        private void OnChangedConnectionStatus(object sender, bool isConnected)
        {
            RunOnUiThread(() =>
            {
                _textStatus.Text = isConnected ? "Connected" : "Connecting...";
                Toast.MakeText(this, $"Cica is {(isConnected ? "connected" : "disconnected")}.", ToastLength.Short).Show();
                if (isConnected)
                {
                    _lasttimeSendData = DateTime.Now;
                    OnUpdateControler();
                    _handlerControl.PostDelayed(OnUpdateControler, 100);
                }
                else
                {
                    _handlerControl.RemoveCallbacks(OnUpdateControler);
                    _textFrontBattery.Text = "----";
                    _textRearBattery.Text = "----";
                }
            });
        }

        private void OnUpdateControler()
        {
            RunOnUiThread(() =>
            {
                if (_isUpdate)
                {
                    _bcoreManager.SetMotorSpeed(_fb, _lr, _ro);
                    _isUpdate = false;
                }
                _handlerControl.PostDelayed(OnUpdateControler, 100);
            });
        }
    }
}



