using UnityEngine;

/// <summary>
/// Centralised, lazy player lookup to avoid repeated FindObjectOfType calls per frame.
/// </summary>
public static class PlayerCache
{
    private static BeanController bean;
    private static FirstPersonControllerSimple fps;

    public static void Register(BeanController controller)
    {
        if (controller != null)
        {
            bean = controller;
        }
    }

    public static void Unregister(BeanController controller)
    {
        if (bean == controller)
        {
            bean = null;
        }
    }

    public static void Register(FirstPersonControllerSimple controller)
    {
        if (controller != null)
        {
            fps = controller;
        }
    }

    public static void Unregister(FirstPersonControllerSimple controller)
    {
        if (fps == controller)
        {
            fps = null;
        }
    }

    public static BeanController GetBean(bool searchScene = true)
    {
        if (bean != null && bean.isActiveAndEnabled)
        {
            return bean;
        }

        if (searchScene)
        {
            bean = Object.FindObjectOfType<BeanController>();
        }

        return bean != null && bean.isActiveAndEnabled ? bean : null;
    }

    public static FirstPersonControllerSimple GetFps(bool searchScene = true)
    {
        if (fps != null && fps.isActiveAndEnabled)
        {
            return fps;
        }

        if (searchScene)
        {
            fps = Object.FindObjectOfType<FirstPersonControllerSimple>();
        }

        return fps != null && fps.isActiveAndEnabled ? fps : null;
    }

    public static Transform ResolvePlayerTransform()
    {
        var beanPlayer = GetBean();
        if (beanPlayer != null) return beanPlayer.transform;

        var fpsPlayer = GetFps();
        return fpsPlayer != null ? fpsPlayer.transform : null;
    }
}
