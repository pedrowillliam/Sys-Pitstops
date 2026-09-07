using Microsoft.AspNetCore.Mvc.ModelBinding;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Contracts;

/// <summary>
/// Query strings never pass through the JSON converter, so without this the API
/// would document <c>status=IN_YARD</c> and accept only <c>status=InYard</c> —
/// the generated TypeScript client would send the documented value and get 400.
/// </summary>
public class LabelEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType)
            ?? context.Metadata.ModelType;

        return type.IsEnum ? new LabelEnumModelBinder(type) : null;
    }
}

public class LabelEnumModelBinder(Type enumType) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var provided = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (provided == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, provided);

        var raw = provided.FirstValue;

        // An absent optional filter is not an error; a nullable enum stays null.
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Task.CompletedTask;
        }

        if (EnumExtensions.TryParseLabel(enumType, raw, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            $"Valor inválido: '{raw}'. Esperado um de: {string.Join(", ", Labels(enumType))}.");

        return Task.CompletedTask;
    }

    private static IEnumerable<string> Labels(Type enumType) =>
        Enum.GetNames(enumType).Select(name =>
            EnumExtensions.TryParseLabel(enumType, name, out var parsed) && parsed is not null
                ? LabelOf(parsed)
                : name);

    private static string LabelOf(object value) =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString()!);
}
