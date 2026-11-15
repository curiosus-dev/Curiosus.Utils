using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
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

namespace Curiosus.Tools.Web.Controllers
{
    /// <summary>
    /// Basic controller for ASP.NET Core MVC.
    /// </summary>
    public abstract class MVCBaseController : Controller
    {
        private ILogger? _logger;

        /// <summary>
        /// Logger with the category of the actual controller (<c>HomeController</c>, not <c>MVCBaseController</c>).
        /// </summary>
        protected ILogger Logger =>
            _logger ??= HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

        /// <summary>
        /// Редирект по указанному адресу либо на домашнюю страницу
        /// </summary>
        /// <param name="returnUrl">Адрес куда надо переадресовать</param>
        protected ActionResult RedirectInternal(string? returnUrl = null)
        {
            if (!String.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);

            var homeControllerName = "Home";
            var indexActionName = "Index";
            return RedirectToAction(indexActionName, homeControllerName);
        }
        
        /// <summary>
        /// Страница с сообщением об ошибке.
        /// </summary>
        /// <param name="error">Тип ошибки</param>
        [AllowAnonymous]
        public ActionResult ErrorViewWithCode(SiteError error)
        {
            return ErrorView(error.ToLocalizedString());
        }
        
        /// <summary>
        /// Страница с ошибкой
        /// </summary>
        [AllowAnonymous]
        public ActionResult ErrorViewParams(string fmt, params object[] args)
        {
            return ErrorView(String.Format(fmt, args));
        }
        
        /// <summary>
        /// Страница с сообщением об ошибке.
        /// Возвращает PartialView если ajax запрос.
        /// </summary>
        /// <param name="errorText">Текст ошибки</param>
        [AllowAnonymous]
        public ActionResult ErrorView(string? errorText = null)
        {
            var message = String.IsNullOrWhiteSpace(errorText)
                ? LNG.UnexpectedError
                : errorText;

            var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var ex = exceptionHandlerPathFeature?.Error;

            if (ex != null)
            {
                switch (ex)
                {
                    case BadHttpRequestException _:
                        message = ex.Message;
                        Logger.LogWarning(ex, "Connection was closed by the client while sending response: {Error}", ex.Message);
                        break;
                    case TaskCanceledException _:
                    case OperationCanceledException _:
                        message = ex.Message;
                        Logger.LogWarning(ex, "Request was canceled by user");
                        break;
                    default:
                        Logger.LogError(ex, "Unhandled error: {Error}", ex.Message);
                        break;
                }
            }
            else
            {
                Logger.LogWarning("{ErrorText}", message);
            }

            ViewBag.ErrorText = message;
            
            if (HttpContext.Request.IsAjaxRequest())
            {
                HttpContext.Response.StatusCode = 200;
                ViewBag.ErrorText = String.IsNullOrWhiteSpace(ex?.Message) 
                    ? message
                    : ex.Message;
                return PartialView("AjaxError");
            }

            var errorViewName = "Error";
            return View(errorViewName);
        }
        
        public ActionResult AjaxGridErrorResponse(SiteError error)
        {
            return AjaxGridErrorResponse(null, error.ToLocalizedString());
        }

        public ActionResult AjaxGridErrorResponse(string errorText)
        {
            return AjaxGridErrorResponse(null, errorText);
        }

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
        
        public ActionResult AjaxError(string errorText)
        {
            return Content(errorText);
        }

        public ActionResult AjaxError(SiteError error)
        {
            return AjaxError(error.ToLocalizedString());
        }

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
