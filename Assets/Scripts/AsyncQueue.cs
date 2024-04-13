using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class AsyncQueue
{
	private ConcurrentQueue<Action> queue = new();
	private int activeJobs;
	private readonly string name;
	private readonly Func<int> maxActionsGetter;

	public AsyncQueue(string name, Func<int> maxActionsGetter)
	{
		this.name = name;
		this.maxActionsGetter = maxActionsGetter;
	}

	public void Register(Action action) => queue.Enqueue(action);

	public void Release() => Interlocked.Decrement(ref activeJobs);

	public void Tick()
	{
		Debug.Log($"Performing {Mathf.Min(queue.Count,maxActionsGetter.Invoke()-activeJobs)} {name} jobs");
		while (activeJobs < maxActionsGetter.Invoke() && queue.Any())
		{
			if (queue.TryDequeue(out var action))
			{
				Perform(action);
			}
		}
	}

	private void Perform(Action action)
	{
		activeJobs++;
		action?.Invoke();
	}
}