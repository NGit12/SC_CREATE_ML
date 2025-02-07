using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class UnityMainThread : MonoBehaviour
{
    private static UnityMainThread instance;
    private static readonly Queue<Action> executeOnMainThread = new Queue<Action>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public static async Task<T> ExecuteInUpdate<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        lock (executeOnMainThread)
        {
            executeOnMainThread.Enqueue(() =>
            {
                try
                {
                    T result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
        }
        return await tcs.Task;
    }

    private void Update()
    {
        while (executeOnMainThread.Count > 0)
        {
            Action action;
            lock (executeOnMainThread)
            {
                action = executeOnMainThread.Dequeue();
            }
            action.Invoke();
        }
    }
}
