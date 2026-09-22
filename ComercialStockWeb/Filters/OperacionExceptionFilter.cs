using System.Data.Common;
using Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Web.Filters;

public sealed class OperacionExceptionFilter(IModelMetadataProvider metadata, ILogger<OperacionExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not (ReglaNegocioException or DbException)) return;
        var business = context.Exception as ReglaNegocioException;
        if (business is null) logger.LogError(context.Exception, "Fallo en una operación comercial.");
        var data = new ViewDataDictionary(metadata, context.ModelState)
        {
            ["Mensaje"] = business?.Message ?? "No pudimos completar la operación. No se confirmó ningún cambio parcial. Intenta nuevamente; si persiste, contacta al administrador."
        };
        context.Result = new ViewResult { ViewName = "~/Views/Shared/OperacionError.cshtml", ViewData = data, StatusCode = business is null ? 503 : 409 };
        context.ExceptionHandled = true;
    }
}
