using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace MechanumCica4WD.Android.Views.Controls
{
    public class StickControllerView : View
    {
        #region const

        private const int BorderWidthDp = 10;

        #endregion

        #region field

        private bool _isSizeInited = false;

        private float _density;

        private Paint _areaPaint;

        private Paint _stickPaint;

        private Path _areaRect;

        private Path _areaRote;

        private int _idRect = -1;

        private int _idRote = -1;

        private float _stickRectBaseX = 0;

        private float _stickRectBaseY = 0;

        private float _stickOffsetRectX = 0;

        private float _stickOffsetRectY = 0;

        private float _stickRoteBaseX = 0;

        private float _stickOffsetRoteX = 0;

        #endregion

        #region property


        private float BorderWidth => BorderWidthDp * _density;

        private int AreaWidth => Width / 2;

        private int SizeBase => Math.Min(AreaWidth, Height);

        private float MoveRange => SizeBase * 0.2f;

        private float StickRadius => SizeBase * 0.15f;

        private float AreaRadius => StickRadius + BorderWidth;

        private float AreaRectCenterX => AreaWidth * 0.5f;

        private float AreaRectCenterY => Height * 0.5f;

        private float AreaRoteCenterX => AreaWidth * 1.5f;

        private float AreaRoteCenterY => Height * 0.5f;

        private float LimitAreaRectX => AreaWidth * 0.95f;

        private float LimitAreaRoteX => AreaWidth * 1.05f;

        private float StickRectCenterX => AreaRectCenterX + _stickOffsetRectX;

        private float StcikRectCenterY => AreaRectCenterY + _stickOffsetRectY;

        private float StickRoteCetnerX => AreaRoteCenterX + _stickOffsetRoteX;

        public bool IsEnableTouch { get; set; }

        public float RectXRate { get; set; }

        public float RectYRate { get; set; }

        public float RoteRate { get; set; }

        #endregion


        public StickControllerView(Context context, IAttributeSet attrs) :
            base(context, attrs)
        {
            Initialize();
        }

        public StickControllerView(Context context, IAttributeSet attrs, int defStyle) :
            base(context, attrs, defStyle)
        {
            Initialize();
        }

        private void Initialize()
        {
            _density = Context.Resources.DisplayMetrics.Density;

            _areaPaint = new Paint()
            {
                Color = Color.White,
                StrokeWidth = BorderWidth,
            };
            _areaPaint.SetStyle(Paint.Style.Stroke);

            _stickPaint = new Paint()
            {
                Color = Color.Rgb(230, 255, 0),
            };

            _isSizeInited = false;
        }

        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);

            UpdateDrawPath();
            _isSizeInited = true;
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            if (Width == 0 || Height == 0 || !_isSizeInited) return;

            Log.Debug("Cica4WD", $"w:{Width}/h{Height}");

            //canvas.DrawPath(_areaRect, _areaPaint);
            canvas.DrawCircle(AreaRectCenterX, AreaRectCenterY, 2*MoveRange, _areaPaint);
            canvas.DrawPath(_areaRote, _areaPaint);

            DrawRectStick(canvas);

            DrawRoteStick(canvas);
        }

        public override bool OnTouchEvent(MotionEvent e)
        {
            if (!IsEnableTouch) return base.OnTouchEvent(e);

            var idx = e.ActionIndex;

            var id = e.GetPointerId(idx);

            var x = e.GetX(idx);

            var y = e.GetY(idx);

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                case MotionEventActions.PointerDown:
                    if (SetPressStickRect(id, x, y)) return true;
                    if (SetPressStickRote(id, x, y)) return true;
                    break;
                case MotionEventActions.Up:
                case MotionEventActions.PointerUp:
                case MotionEventActions.Cancel:
                case MotionEventActions.Outside:
                    SetReleasStickRect(id);
                    SetReleasStickRote(id);
                    break;
                case MotionEventActions.Move:
                    for (var i = 0; i < e.PointerCount; i++)
                    {
                        id = e.GetPointerId(i);
                        x = e.GetX(i);
                        y = e.GetY(i);

                        MovePointRect(id, x, y);
                        MovePointRote(id, x, y);
                    }
                    break;
            }

            Invalidate();

            return base.OnTouchEvent(e);
        }

        public void SetStickRate(float rectX, float rectY, float rote)
        {
            if (IsEnableTouch) return;

            RectXRate = rectX;
            RectYRate = rectY;
            RoteRate = rote;

            _stickOffsetRectX = MoveRange * rectX;
            _stickOffsetRectY = MoveRange * rectY;
            _stickOffsetRoteX = MoveRange * rote;

            Invalidate();
        }

        #region private method

        #region path

        private void UpdateDrawPath()
        {
            UpdateRectStickPath();

            UpdateHorizontalStickPath();
        }

        private void UpdateRectStickPath()
        {
            _areaRect = new Path();

            _areaRect.MoveTo(AreaRectCenterX - MoveRange, AreaRectCenterY - MoveRange - AreaRadius);
            _areaRect.LineTo(AreaRectCenterX + MoveRange, AreaRectCenterY - MoveRange - AreaRadius);
            _areaRect.ArcTo(
                new RectF(AreaRectCenterX + MoveRange - AreaRadius, AreaRectCenterY - MoveRange - AreaRadius,
                    AreaRectCenterX + MoveRange + AreaRadius, AreaRectCenterY - MoveRange + AreaRadius), 270, 90);
            _areaRect.LineTo(AreaRectCenterX + MoveRange + AreaRadius, AreaRectCenterY + MoveRange);
            _areaRect.ArcTo(
                new RectF(AreaRectCenterX + MoveRange - AreaRadius, AreaRectCenterY + MoveRange - AreaRadius,
                    AreaRectCenterX + MoveRange + AreaRadius, AreaRectCenterY + MoveRange + AreaRadius), 0, 90);
            _areaRect.LineTo(AreaRectCenterX - MoveRange, AreaRectCenterY + MoveRange + AreaRadius);
            _areaRect.ArcTo(
                new RectF(AreaRectCenterX - MoveRange - AreaRadius, AreaRectCenterY + MoveRange - AreaRadius,
                    AreaRectCenterX - MoveRange + AreaRadius, AreaRectCenterY + MoveRange + AreaRadius), 90, 90);
            _areaRect.LineTo(AreaRectCenterX - MoveRange - AreaRadius, AreaRectCenterY - MoveRange);
            _areaRect.ArcTo(
                new RectF(AreaRectCenterX - MoveRange - AreaRadius, AreaRectCenterY - MoveRange - AreaRadius,
                    AreaRectCenterX - MoveRange + AreaRadius, AreaRectCenterY - MoveRange + AreaRadius), 180, 90);
        }

        private void UpdateHorizontalStickPath()
        {
            _areaRote = new Path();

            _areaRote.MoveTo(AreaRoteCenterX - MoveRange, AreaRoteCenterY - AreaRadius);
            _areaRote.LineTo(AreaRoteCenterX + MoveRange, AreaRectCenterY - AreaRadius);
            _areaRote.ArcTo(new RectF(AreaRoteCenterX + MoveRange - AreaRadius, AreaRoteCenterY - AreaRadius,
                AreaRoteCenterX + MoveRange + AreaRadius, AreaRoteCenterY + AreaRadius), 270, 180);
            _areaRote.LineTo(AreaRoteCenterX - MoveRange, AreaRectCenterY + AreaRadius);
            _areaRote.ArcTo(new RectF(AreaRoteCenterX - MoveRange - AreaRadius, AreaRoteCenterY - AreaRadius,
                AreaRoteCenterX + MoveRange - AreaRadius, AreaRoteCenterY + AreaRadius), 90, 180);
        }

        #endregion

        #region rect stick

        private void DrawRectStick(Canvas canvas)
        {
            canvas.DrawCircle(AreaRectCenterX + _stickOffsetRectX, AreaRectCenterY + _stickOffsetRectY, StickRadius, _stickPaint);
        }

        private bool SetPressStickRect(int id, float x, float y)
        {
            if (id == _idRect) return true;
            if (_idRect >= 0 || x > LimitAreaRectX) return false;

            _idRect = id;
            _stickRectBaseX = x;
            _stickRectBaseY = y; 

            return true;
        }

        private void SetReleasStickRect(int id)
        {
            if (_idRect != id) return;

            _idRect = -1;
            _stickOffsetRectX = 0;
            _stickOffsetRectY = 0;
        }

        private void MovePointRect(int id, float x, float y)
        {
            if (_idRect != id) return;

            var offsetX = x - _stickRectBaseX;
            var offsetY = y - _stickRectBaseY;

            if (Math.Abs(offsetX) > MoveRange)
            {
                offsetX = MoveRange * (offsetX < 0 ? -1 : 1);
            }

            if (Math.Abs(offsetY) > MoveRange)
            {
                offsetY = MoveRange * (offsetY < 0 ? -1 : 1);
            }

            _stickOffsetRectX = offsetX;
            _stickOffsetRectY = offsetY;
        }

        #endregion

        #region stick rote

        private void DrawRoteStick(Canvas canvas)
        {
            canvas.DrawCircle(AreaRoteCenterX + _stickOffsetRoteX, AreaRoteCenterY, StickRadius, _stickPaint);
        }

        private bool SetPressStickRote(int id, float x, float y)
        {
            if (id == _idRote) return true;
            if (_idRote >= 0 || x < LimitAreaRoteX) return false;

            _idRote = id;
            _stickRoteBaseX = x;

            return true;
        }

        private void SetReleasStickRote(int id)
        {
            if (_idRote != id) return;

            _idRote = -1;
            _stickOffsetRoteX = 0;
        }

        private void MovePointRote(int id, float x, float y)
        {
            if (_idRote != id) return;

            var offsetX = x - _stickRoteBaseX;

            if (Math.Abs(offsetX) > MoveRange)
            {
                offsetX = MoveRange * (offsetX < 0 ? -1 : 1);
            }

            _stickOffsetRoteX = offsetX;
        }

        #endregion

        #endregion
    }
}