using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public class ComputeShaderController
{
	private ConcurrentQueue<Action> queue = new();
	private int activeJobs;
	private const int MAX_COUNTER = 10;

	public void Register(Action action)
	{
		queue.Enqueue(action);
	}

	public void Release()
	{
		Interlocked.Decrement(ref activeJobs);
	}

	public void Tick()
	{
		while (activeJobs < MAX_COUNTER && queue.Any())
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