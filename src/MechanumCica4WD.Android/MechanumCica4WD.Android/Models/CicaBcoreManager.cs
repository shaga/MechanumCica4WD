﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Util;

namespace MechanumCica4WD.Android.Models
{
    public enum EBcoreType
    {
        Front,
        Rear,
    }

    public class ReadBatteryEventArgs : EventArgs
    {
        public EBcoreType Type { get; }

        public int Voltage { get; }

        public ReadBatteryEventArgs(bool isFront, int voltage)
        {
            Type = isFront ? EBcoreType.Front : EBcoreType.Rear;
            Voltage = voltage;
        }
    }

    public class CicaBcoreManager : BluetoothGattCallback
    {
        #region const

        private const string FrontBcoreAddress = "00:07:80:38:FE:66";

        private const string RearBcoreAddress = "00:07:80:38:FE:32";

        private const byte IdxMotorFrontLeft = 0;
        private const byte IdxMotorFrontRight = 1;
        private const byte IdxMotorRearLeft = 1;
        private const byte IdxMotorRearRight = 0;

        public static readonly UUID BcoreService = UUID.FromString("389CAAF0-843F-4D3B-959D-C954CCE14655");
        public static readonly UUID BatteryVol = UUID.FromString("389CAAF1-843F-4D3B-959D-C954CCE14655");
        public static readonly UUID MotorPwm = UUID.FromString("389CAAF2-843F-4D3B-959D-C954CCE14655");

        #endregion

        private readonly Context _context;

        private bool _frontIsConnected;

        private bool _rearIsConnected;

        private bool _frontIsConnecting;

        private bool _rearIsConnecting;

        private BluetoothGatt _frontGatt;

        private BluetoothGatt _rearGatt;

        private BluetoothGattCharacteristic _frontMotor;

        private BluetoothGattCharacteristic _rearMotor;

        private BluetoothGattCharacteristic _frontBattery;

        private BluetoothGattCharacteristic _rearBattery;

        private EBcoreType _readBatteryType = EBcoreType.Front;

        private DateTime _lastControllerUpdate;

        private readonly Handler _handler;

        private int _fl = 128;
        private int _fr = 128;
        private int _bl = 128;
        private int _br = 128;

        private BluetoothManager BluetoothManager => _context?.GetSystemService(Context.BluetoothService) as BluetoothManager;

        private BluetoothAdapter BluetoothAdapter => BluetoothManager?.Adapter;

        public bool IsConnected => _frontIsConnected && _rearIsConnected;

        public bool IsConnecting => _frontIsConnecting && _rearIsConnecting;

        public event EventHandler<bool> ConnectionStatusChanged;

        public event EventHandler<ReadBatteryEventArgs> BatteryVoltageRead; 

        public CicaBcoreManager(Context context)
        {
            _context = context;
            _handler = new Handler();
        }

        public void Connect()
        {
            if (!_frontIsConnected)
            {
                ConnectBcore(FrontBcoreAddress, ref _frontGatt, ref _frontIsConnecting);
            }

            if (!_rearIsConnected)
            {
                ConnectBcore(RearBcoreAddress, ref _rearGatt, ref _rearIsConnecting);
            }
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                _handler.RemoveCallbacks(ReadBattery);
            }

            if (_frontIsConnected)
            {
                _frontGatt.Disconnect();
                _frontIsConnected = false;
            }

            if (_rearIsConnected)
            {
                _rearGatt.Disconnect();
                _rearIsConnected = false;
            }
        }

        public override void OnConnectionStateChange(BluetoothGatt gatt, GattStatus status, ProfileState newState)
        {
            base.OnConnectionStateChange(gatt, status, newState);

            var before = IsConnected;

            switch (newState)
            {
                // bCoreと接続
                case ProfileState.Connected:
                    // サービス検索
                    gatt.DiscoverServices();
                    break;
                // bCoreと切断
                case ProfileState.Disconnected:
                    if (GattIsFront(gatt))
                    {
                        // 前bCoreと切断
                        _frontIsConnected = false;

                        if (_rearIsConnected)
                        {
                            // 後bCoreが接続中なら後ろのモーターを停止
                            WriteMotorData(false, 0, 0x80);
                            WriteMotorData(false, 1, 0x80);
                            _bl = 128;
                            _br = 128;
                        }

                        ConnectBcore(FrontBcoreAddress, ref _frontGatt, ref _frontIsConnecting);
                    }
                    else if (GattIsRear(gatt))
                    {
                        // 後bCoreと切断
                        _rearIsConnected = false;

                        if (_frontIsConnected)
                        {
                            // 前bCoreが接続中なら前のモーターを停止
                            WriteMotorData(true, 0, 0x80);
                            WriteMotorData(true, 1, 0x80);
                            _fr = 128;
                            _fl = 128;
                        }

                        ConnectBcore(RearBcoreAddress, ref _rearGatt, ref _rearIsConnecting);
                    }
                    break;
            }

            if (!IsConnected && before)
            {
                ConnectionStatusChanged?.Invoke(this, false);
                if (_readBatteryType == EBcoreType.Front) _handler.RemoveCallbacks(ReadBattery);
            }
        }

        public override void OnServicesDiscovered(BluetoothGatt gatt, GattStatus status)
        {
            base.OnServicesDiscovered(gatt, status);


            var service = gatt.Services.FirstOrDefault(s => s.Uuid.ToString().ToUpper() == BcoreService.ToString().ToUpper());

            if (service == null) return;

            if (GattIsFront(gatt))
            {
                _frontIsConnected = true;
                _frontIsConnecting = false;
                _frontMotor = service.GetCharacteristic(MotorPwm);
                _frontBattery = service.GetCharacteristic(BatteryVol);
            }
            else if (GattIsRear(gatt))
            {
                _rearIsConnected = true;
                _rearIsConnecting = false;
                _rearMotor = service.GetCharacteristic(MotorPwm);
                _rearBattery = service.GetCharacteristic(BatteryVol);
            }

            if (!IsConnected) return;

            ConnectionStatusChanged?.Invoke(this, true);
            _readBatteryType = EBcoreType.Front;
            ReadBattery();
            _lastControllerUpdate = DateTime.Now;

            ReadBattery();
        }

        public override void OnCharacteristicRead(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
        {
            base.OnCharacteristicRead(gatt, characteristic, status);

            var data = characteristic.GetValue();
            var voltage = data[0] | (data[1] << 8);

            BatteryVoltageRead?.Invoke(this, new ReadBatteryEventArgs(GattIsFront(gatt), voltage));

            if (GattIsFront(gatt))
            {
                ReadBattery();
            }
            else
            {
                _handler.PostDelayed(ReadBattery, 30000);
            }
        }

        public async void SetMotorSpeed(float fb, float lr, float ro, bool isForce = false)
        {
            if (!IsConnected) return;

            var now = DateTime.Now;

            if (now - _lastControllerUpdate < TimeSpan.FromMilliseconds(40) && !isForce) return;

            var lf = fb - lr - ro;
            var rf = fb + lr + ro;
            var lb = fb + lr - ro;
            var rb = fb - lr + ro;

            var lfm = GetMotorPower(lf);
            var rfm = GetMotorPower(rf, true);
            var lbm = GetMotorPower(lb, true);
            var rbm = GetMotorPower(rb);

            if (lfm != _fl)
            {
                WriteMotorData(true, IdxMotorFrontLeft, lfm);
                _fl = lfm;
                await Task.Delay(15);
            }
            if (rbm != _br)
            {
                WriteMotorData(false, IdxMotorRearRight, rbm);
                _br = rbm;
                await Task.Delay(15);
            }
            if (rfm != _fr)
            {
                WriteMotorData(true, IdxMotorFrontRight, rfm);
                _fr = rfm;
                await Task.Delay(15);
            }
            if (lbm != _bl)
            {
                WriteMotorData(false, IdxMotorRearLeft, lbm);
                _bl = lbm;
            }
            Log.Debug("Cica4WD", $"LF:{lfm}/RF:{rfm}/LB:{lbm}/RB:{rbm}");

            _lastControllerUpdate = now;
        }

        private void ConnectBcore(string address, ref BluetoothGatt gatt, ref bool isConnecting)
        {
            if (isConnecting) return;

            isConnecting = true;

            if (gatt == null)
            {
                var device = BluetoothAdapter.GetRemoteDevice(address);
                gatt = device.ConnectGatt(_context, true, this);
            }
            else
            {
                gatt.Connect();
            }

        }

        private int GetMotorPower(float src, bool isInvert = false)
        {
            if (src > 1.0) src = 1.0f;
            else if (src < -1.0) src = -1.0f;

            return (int) (128 + 128 * src * (isInvert ? -1 : 1));
        }

        private bool GattIsFront(BluetoothGatt gatt)
        {
            return gatt.Device.Address.ToUpper().Equals(FrontBcoreAddress);
        }

        private bool GattIsRear(BluetoothGatt gatt)
        {
            return gatt.Device.Address.ToUpper().Equals(RearBcoreAddress);
        }

        private void WriteMotorData(bool isFront, byte idx, int power)
        {
            if (power < 0) power = 0;
            else if (power > 255) power = 255;

            var data = new byte[] {idx, (byte) power};

            WriteData(isFront ? _frontGatt : _rearGatt, isFront ? _frontMotor : _rearMotor, data );
        }

        private void WriteData(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] data)
        {
            if (gatt == null || characteristic == null ||
                !characteristic.Properties.HasFlag(GattProperty.Write) &&
                !characteristic.Properties.HasFlag(GattProperty.WriteNoResponse)) return;

            characteristic.SetValue(data);
            gatt.WriteCharacteristic(characteristic);
        }

        private void ReadData(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
        {
            if (gatt == null || characteristic == null || !characteristic.Properties.HasFlag(GattProperty.Read)) return;

            gatt.ReadCharacteristic(characteristic);
        }

        private void ReadBattery()
        {
            if (_readBatteryType == EBcoreType.Front && _frontIsConnected)
            {
                ReadData(_frontGatt, _frontBattery);
                _readBatteryType = EBcoreType.Rear;
            }
            else if (_readBatteryType == EBcoreType.Rear && _rearIsConnected)
            {
                ReadData(_rearGatt, _rearBattery);
                _readBatteryType = EBcoreType.Front;
            }
        }
    }
}