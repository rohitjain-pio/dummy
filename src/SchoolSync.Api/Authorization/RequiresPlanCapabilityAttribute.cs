using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Commercial;

namespace SchoolSync.Api.Authorization;

// BRL-063 (CT-503): the tier axis, applied as an action filter. Action filters run AFTER
// authorization, so RBAC is always evaluated first and this is a genuinely second, independent check
// — a caller the role rules refuse gets 403 and never reaches here; a caller the role rules allow is
// still refused with 402 ERR-802 if the institution's plan lacks the capability (TC-707).
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequiresPlanCapabilityAttribute(string capability) : Attribute, IFilterFactory
{
    public string Capability { get; } = capability;
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new PlanCapabilityFilter(Capability, serviceProvider.GetRequiredService<IPlanEntitlements>());

    private sealed class PlanCapabilityFilter(string capability, IPlanEntitlements entitlements) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!await entitlements.IncludesAsync(context.HttpContext.User.InstitutionId(), capability, context.HttpContext.RequestAborted))
            {
                context.Result = PlanCapabilityDenied();
                return;
            }
            await next();
        }
    }

    public static ObjectResult PlanCapabilityDenied() => new(ErrCatalogue.Err802) { StatusCode = StatusCodes.Status402PaymentRequired };
}
