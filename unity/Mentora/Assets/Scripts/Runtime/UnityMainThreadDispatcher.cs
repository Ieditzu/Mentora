using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> ExecutionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher instance;
    private readonly System.Collections.Generic.List<Action> workBuffer = new System.Collections.Generic.List<Action>(16);

    public void Enqueue(Action action)
    {
        lock (ExecutionQueue)
        {
            ExecutionQueue.Enqueue(action);
        }
    }

    public static bool IsInitialized => instance != null;

    public static UnityMainThreadDispatcher Instance()
    {
        if (instance == null)
        {
            throw new Exception("UnityMainThreadDispatcher not initialized. Call Initialize() from main thread first.");
        }
        return instance;
    }

    public static void Initialize()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("UnityMainThreadDispatcher");
            instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Update()
    {
        lock (ExecutionQueue)
        {
            if (ExecutionQueue.Count == 0)
            {
                return;
            }

            while (ExecutionQueue.Count > 0)
            {
                workBuffer.Add(ExecutionQueue.Dequeue());
            }
        }

        for (int i = 0; i < workBuffer.Count; i++)
        {
            try
            {
                workBuffer[i].Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        workBuffer.Clear();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
