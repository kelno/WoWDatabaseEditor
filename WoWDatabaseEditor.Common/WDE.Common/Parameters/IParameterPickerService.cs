using System.Threading.Tasks;
using WDE.Module.Attributes;

namespace WDE.Common.Parameters;

[UniqueProvider]
public interface IParameterPickerService
{
    public Task<(T? value, bool ok)> PickParameter<T>(IParameter<T> parameter, T currentValue, object? context = null) where T : notnull;
}