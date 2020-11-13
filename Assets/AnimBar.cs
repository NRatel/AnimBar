using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

public class AnimBar : MonoBehaviour
{
    [SerializeField]
    private Image m_Fill;                //动画填充条
    [SerializeField]
    private Text m_Label;
    [SerializeField]
    private float m_OnceDuration = 0.1f;    //控制播放速度（动画填充/消失一条的时间）
    [SerializeField]
    private Ease m_AniEase;                 //动画曲线

    private RectTransform m_FillRectTransform;
    private float m_FillTotalWidth;         //填充条总宽度（注意保持预设进度为1）
    private Sequence m_Sequence;            //动画队列
    private int m_CurIntegralPart;          //整数部分（由动画维护）
    
    public Action<float> onUpdate;
    public Action<int> onIntegralPartChanged;
    public Action<float> onCompleteFinally;

    public void Awake()
    {
        m_Fill.SetNativeSize();
        m_FillRectTransform = m_Fill.rectTransform;
        this.m_FillTotalWidth = m_FillRectTransform.rect.width;
    }

    public void Init(float initValue)
    {
        //先停止当前动画队列
        if (m_Sequence != null)
        {
            m_Sequence.Kill(false);
            m_Sequence = null;
        }

        int initIntegralPart = Mathf.FloorToInt(initValue);
        float initFractionalPart = initValue - initIntegralPart;

        m_CurIntegralPart = initIntegralPart;
        SetWidthWithFractionalPart(initFractionalPart);
    }

    public void Refresh(float newValue)
    {
        int oldIntegralPart = m_CurIntegralPart;
        float oldFractionalPart = GetCurFractionalPart();

        int newIntegralPart = Mathf.FloorToInt(newValue);
        float newFractionalPart = newValue - newIntegralPart;
        
        StartAni(oldIntegralPart, oldFractionalPart, newIntegralPart, newFractionalPart);
    }
    
    //根据小数部分设置宽度
    private void SetWidthWithFractionalPart(float fractionalPart)
    {
        m_FillRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_FillTotalWidth * fractionalPart);
    }

    //获取当前显示的百分比（小数部分）
    private float GetCurFractionalPart()
    {
        return m_FillRectTransform.rect.width / m_FillTotalWidth;
    }

    private void StartAni(int oldIntegralPart, float oldFractionalPart, int newIntegralPart, float newFractionalPart)
    {
        //先停止当前动画队列
        if (m_Sequence != null)
        {
            m_Sequence.Kill(false);
            m_Sequence = null;
        }

        //新建动画队列
        m_Sequence = DOTween.Sequence();

        //决定方向
        float totalOld = oldIntegralPart + oldFractionalPart;
        float totalNew = newIntegralPart + newFractionalPart;

        if (totalNew == totalOld) { return; }

        if (totalNew > totalOld)
        {
            //增长
            int deltaIntegralPart = newIntegralPart - oldIntegralPart;
            if (deltaIntegralPart > 0)
            {
                //1、整数部分+1，小数部分归0
                m_Sequence.AppendCallback(() => { SetWidthWithFractionalPart(oldFractionalPart); });
                m_Sequence.Append(CreateSingleAni(oldFractionalPart, 1, () => { m_CurIntegralPart = m_CurIntegralPart + 1; }));
                //2、整数部分+deltaIntegralPart
                for (int i = 1; i < deltaIntegralPart; i++)
                {
                    m_Sequence.AppendCallback(() => { SetWidthWithFractionalPart(0); });
                    m_Sequence.Append(CreateSingleAni(0, 1, () => { m_CurIntegralPart = m_CurIntegralPart + 1; }));
                }
                //3、小数部分+newFractionalPart
                m_Sequence.AppendCallback(() => { SetWidthWithFractionalPart(0); });
                m_Sequence.Append(CreateSingleAni(0, newFractionalPart, null));
            }
            else
            {
                //1、小数部分+newFractionalPart
                m_Sequence.AppendCallback(() => { SetWidthWithFractionalPart(oldFractionalPart); });
                m_Sequence.Append(CreateSingleAni(oldFractionalPart, newFractionalPart, null));
            }
        }
        else
        {
            //减少
            int deltaIntegralPart = newIntegralPart - oldIntegralPart;
            if (deltaIntegralPart < 0)
            {
                //1、小数部分归0
                m_Sequence.AppendCallback(() => { SetWidthWithFractionalPart(oldFractionalPart); });
                m_Sequence.Append(CreateSingleAni(oldFractionalPart, 0, () => { m_CurIntegralPart = m_CurIntegralPart - 1; }));
                //2、整数部分-Mathf.Abs(deltaIntegralPart)
                for (int i = 1; i < Mathf.Abs(deltaIntegralPart); i++)
                {
                    m_Sequence.AppendCallback(() => { SetWidthWithFractionalPart(1); });
                    m_Sequence.Append(CreateSingleAni(1, 0, () => { m_CurIntegralPart = m_CurIntegralPart - 1; }));
                }
                //3、小数部分-newFractionalPart
                m_Sequence.AppendCallback(() => { SetWidthWithFractionalPart(1); });
                m_Sequence.Append(CreateSingleAni(1, newFractionalPart, null));
            }
            else
            {
                //1、小数部分-newFractionalPart
                m_Sequence.AppendCallback(() => { SetWidthWithFractionalPart(oldFractionalPart); });
                m_Sequence.Append(CreateSingleAni(oldFractionalPart, newFractionalPart, null));
            }
        }
        
        m_Sequence.OnComplete(() =>
        {
            float value = m_CurIntegralPart + GetCurFractionalPart();
            onCompleteFinally?.Invoke(value);
        });
        m_Sequence.OnUpdate(() =>
        {
            float value = m_CurIntegralPart + GetCurFractionalPart();
            m_Label.text = value.ToString();
            onUpdate?.Invoke(value);
        });

        m_Sequence.Play();
    }

    private Tweener CreateSingleAni(float startValue, float endValue, Action onComplete)
    {
        float endWidth = endValue * m_FillTotalWidth;

        Tweener tweener = m_FillRectTransform.DOSizeDelta(new Vector2(endWidth, m_FillRectTransform.rect.height), Mathf.Abs(endValue - startValue) * m_OnceDuration, false);

        tweener.OnComplete(() =>
        {
            SetWidthWithFractionalPart(endValue);
            onComplete?.Invoke();
            onIntegralPartChanged?.Invoke(m_CurIntegralPart);
        });

        tweener.SetEase(m_AniEase);
        return tweener;
    }

    private string initStr = "0";
    private string refreshStr = "0";
    private bool isInited = false;
    public void OnGUI()
    {
        GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
        textFieldStyle.fontSize = 24;

        initStr = GUI.TextField(new Rect(100, 0, 100, 50), initStr, textFieldStyle);
        refreshStr = GUI.TextField(new Rect(100, 100, 100, 50), refreshStr, textFieldStyle);

        if (GUI.Button(new Rect(0, 0, 100, 50), "初始化"))
        {
            bool result = float.TryParse(initStr, out float initValue);
            if (result)
            {
                Init(initValue);
                isInited = true;
            }
        }
        
        if (GUI.Button(new Rect(0, 100, 100, 50), "刷新"))
        {
            if (!isInited) { return; }
            bool result = float.TryParse(refreshStr, out float destValue);
            if (result)
            {
                this.Refresh(destValue);
            }
        }
    }
}
