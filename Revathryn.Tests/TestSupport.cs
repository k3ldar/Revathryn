using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace Revathryn.Tests
{
    /// <summary>Shared plumbing used by MVC controller tests.</summary>
    [ExcludeFromCodeCoverage]
    internal static class ControllerTestSupport
    {
        /// <summary>Creates a controller context backed by a fresh HTTP context.</summary>
        public static ControllerContext CreateControllerContext(out DefaultHttpContext httpContext)
        {
            httpContext = new DefaultHttpContext();
            ActionContext actionContext = new(httpContext, new RouteData(), new ControllerActionDescriptor());
            return new ControllerContext(actionContext);
        }
    }

    /// <summary>Test-only URL helper that avoids routing dependencies.</summary>
    [ExcludeFromCodeCoverage]
    internal sealed class FakeUrlHelper : IUrlHelper
    {
        private readonly ActionContext _actionContext;

        public FakeUrlHelper(ActionContext actionContext)
        {
            _actionContext = actionContext;
        }

        public ActionContext ActionContext => _actionContext;

        public string Action(UrlActionContext actionContext)
        {
            return "/test/" + actionContext.Action;
        }

        public string Content(string contentPath)
        {
            return contentPath;
        }

        public bool IsLocalUrl(string url)
        {
            return true;
        }

        public string RouteUrl(UrlRouteContext routeContext)
        {
            return "/test/route";
        }

        public string Link(string routeName, object values)
        {
            return "/test/link";
        }
    }

    /// <summary>Test-only temp data provider that persists values in memory.</summary>
    [ExcludeFromCodeCoverage]
    internal sealed class FakeTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object> _data = new();

        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return _data;
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _data.Clear();

            foreach (KeyValuePair<string, object> pair in values)
            {
                _data[pair.Key] = pair.Value;
            }
        }
    }
}
