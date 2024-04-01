using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using StuartHeathTools;
using UnityEngine;

public class MainThreadDispatcher : GenericUnitySingleton<MainThreadDispatcher>
{
	private static readonly Queue<Action> ExecutionQueue = new Queue<Action>();
	public TaskScheduler Sceduler { get; private set; }

	private void Start()
	{
		Sceduler = TaskScheduler.FromCurrentSynchronizationContext();
	}

	public void Enqueue(Action action)
	{
		lock (ExecutionQueue)
		{
			ExecutionQueue.Enqueue(action);
		}
	}

	void Update()
	{
		lock (ExecutionQueue)
		{
			while (ExecutionQueue.Count > 0)
			{
				ExecutionQueue.Dequeue().Invoke();
			}
		}
	}
	
	public void StartCoroutineOnMain(IEnumerator coroutine)
	{
		StartCoroutine(coroutine);
	}
}