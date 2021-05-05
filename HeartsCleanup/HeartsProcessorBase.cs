using System.Collections;
using UnityEngine;

public abstract class HeartsProcessorBase : ScriptableObject
{
    public virtual void OnInitialize(HeartsManager manager)
    {
    }
    public virtual void OnUpdate(HeartsManager manager)
    {
    }
    public virtual void OnLateUpdate(HeartsManager manager)
    {
    }

    public virtual void OnRender(HeartsManager manager)
    {
    }

    public virtual void OnTeardown(HeartsManager manager)
    {
    }
}

