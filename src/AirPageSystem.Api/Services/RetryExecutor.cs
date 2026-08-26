using AirPageSystem.Api.Models;

namespace AirPageSystem.Api.Services;

public sealed class RetryExecutor(ILogger<RetryExecutor> logger)
{
    public async Task<(T Value,int Attempts)> RunAsync<T>(Func<CancellationToken,Task<T>> action,RetryPolicyDefinition policy,string operation,CancellationToken ct)
    {
        Exception? last=null;var attempts=Math.Clamp(policy.MaxAttempts,1,10);var delay=Math.Clamp(policy.InitialDelayMs,0,60000);
        for(var i=1;i<=attempts;i++)
        {
            try{return(await action(ct),i);}catch(Exception ex) when(i<attempts && ex is not OperationCanceledException)
            {
                last=ex;logger.LogWarning("{Operation} failed on attempt {Attempt}/{MaxAttempts}: {Error}",operation,i,attempts,ex.Message);
                if(delay>0)await Task.Delay(delay,ct);
                delay=Math.Min(Math.Clamp(policy.MaxDelayMs,0,300000),(int)Math.Ceiling(delay*Math.Clamp(policy.BackoffFactor,1,10)));
            }
        }
        throw new InvalidOperationException($"{operation}在{attempts}次尝试后失败：{last?.Message}",last);
    }
}
