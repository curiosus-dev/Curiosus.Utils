using System;
using System.IO;
using System.Text.Encodings.Web;
using Curiosus.Tools.Web.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.Controllers
{
    /// <summary>
    /// Basic controller for ASP.NET Core MVC.
    /// </summary>
    public abstract class MVCBaseController : Controller
    {
        /// <summary>
        /// Redirects to <paramref name="returnUrl"/> when it is a local URL of this app or an <c>http</c>/<c>https</c> URL to a
        /// host of <see cref="RedirectAllowListOptions"/>, otherwise to Home/Index.
        /// </summary>
        /// <param name="returnUrl">Where to go back, usually from the query or a form: never trusted.</param>
        /// <remarks>
        /// <c>https://evil.example</c>, <c>//evil.example</c>, <c>/\evil.example</c> or <c>javascript:</c> go to Home/Index:
        /// a redirect to them after a login would be an open redirect. Allow the public domains and the front-end address
        /// with <c>services.Configure&lt;RedirectAllowListOptions&gt;(...)</c>.
        /// </remarks>
        protected ActionResult RedirectInternal(string? returnUrl = null)
        {
            if (!String.IsNullOrEmpty(returnUrl))
            {
                if (Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(UrlCharacterEncoder.EncodeIllegalCharacters(returnUrl));

                var allowList = HttpContext.RequestServices.GetService<IOptions<RedirectAllowListOptions>>()?.Value;
                // redirect to the URL that was validated, in its canonical form, not to the raw string
                if (allowList != null && allowList.TryGetAllowedUri(returnUrl, out var allowed))
                    return Redirect(allowed.AbsoluteUri);
            }

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// The error page endpoint, for <c>UseExceptionHandler("/home/ErrorView")</c>: binds nothing from the request and
        /// shows the generic error text. Not limited to GET, because the exception handler re-executes POST requests too.
        /// </summary>
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [ActionName("ErrorView")]
        public ActionResult HandleError() => ErrorView();

        /// <summary>
        /// Shows <paramref name="error"/> on the error page.
        /// </summary>
        [NonAction]
        public ActionResult ErrorViewWithCode(SiteError error)
        {
            return ErrorView(error.ToLocalizedString());
        }

        /// <summary>
        /// Shows a formatted text on the error page.
        /// </summary>
        [NonAction]
        public ActionResult ErrorViewParams(string fmt, params object[] args)
        {
            return ErrorView(String.Format(fmt, args));
        }

        /// <summary>
        /// Logs the exception of the request, if any, and shows the error page, or the <c>AjaxError</c> partial view to an
        /// ajax request.
        /// </summary>
        /// <param name="errorText">Text for the user from the app's code; the generic message when empty.</param>
        /// <remarks>
        /// The exception message never reaches the client: it may contain user input, SQL, paths or host names.
        /// </remarks>
        [NonAction]
        public ActionResult ErrorView(string? errorText = null)
        {
            // the category of the actual controller (HomeController), not of this base class
            var logger = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

            var message = String.IsNullOrWhiteSpace(errorText)
                ? LNG.UnexpectedError
                : errorText;

            var ex = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Error;
            switch (ex)
            {
                case null:
                    break;
                case BadHttpRequestException:
                    logger.LogWarning(ex, "Bad request: {Error}", ex.Message);
                    break;
                case OperationCanceledException:
                    logger.LogInformation(ex, "Request was cancelled");
                    break;
                default:
                    logger.LogError(ex, "Unhandled error: {Error}", ex.Message);
                    break;
            }

            ViewBag.ErrorText = message;

            if (HttpContext.Request.IsAjaxRequest())
            {
                HttpContext.Response.StatusCode = 200;
                return PartialView("AjaxError");
            }

            return View("Error");
        }

        [NonAction]
        public ActionResult AjaxGridErrorResponse(SiteError error)
        {
            return AjaxGridErrorResponse(null, error.ToLocalizedString());
        }

        [NonAction]
        public ActionResult AjaxGridErrorResponse(string errorText)
        {
            return AjaxGridErrorResponse(null, errorText);
        }

        [NonAction]
        public ActionResult AjaxGridErrorResponse(long? rowId, string errorText)
        {
            return AjaxGridResponse(rowId, errorText, new {style = "color: #f00;"});
        }

        // ответы на ajax-запросы грида
        private ActionResult AjaxGridResponse(long? rowId, string? content = null, object? htmlAttributes = null)
        {
            var div = new TagBuilder("div");
            if (rowId.HasValue)
                div.MergeAttribute("data-row-id", rowId.ToString());
            if (htmlAttributes != null)
                div.MergeAttributes(new RouteValueDictionary(htmlAttributes));
            if (!String.IsNullOrEmpty(content))
                div.InnerHtml.SetContent(content);

            div.TagRenderMode = TagRenderMode.Normal;

            using (var writer = new StringWriter())
            {
                div.WriteTo(writer, HtmlEncoder.Default);
                return Content(writer.ToString(), "text/html");
            }
        }
        
        [NonAction]
        public ActionResult AjaxError(string errorText)
        {
            return Content(errorText);
        }

        [NonAction]
        public ActionResult AjaxError(SiteError error)
        {
            return AjaxError(error.ToLocalizedString());
        }

        [NonAction]
        public ActionResult AjaxSuccess()
        {
            return Content("success");
        }

        public override RedirectResult Redirect(string url)
        {
            // перекодируем русские символы, если есть, чтоб избежать "Invalid non-ASCII" исключения
            var encoded = UrlCharacterEncoder.EncodeIllegalCharacters(url);
            return base.Redirect(encoded);
        }
    }
}
