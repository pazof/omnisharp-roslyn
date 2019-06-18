using Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages;
using OmniSharp.Models;

namespace OmniSharp.Dnx
{
    public class DnxRuntimePathResult
    {
        public string Value { get; set; }

        public ErrorMessage Error { get; set; }
    }
}