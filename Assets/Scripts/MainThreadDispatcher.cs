using System;
using System.Collections.Generic;
using StuartHeathTools;
using UnityEngine;

public class MainThreadDispatcher : GenericUnitySingleton<MainThreadDispatcher>
{
	private static readonly Queue<Action> ExecutionQueue = new Queue<Action>();

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
}