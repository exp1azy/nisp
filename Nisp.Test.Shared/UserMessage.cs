using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nisp.Test.Shared
{
    [MemoryPackable]
    public partial struct UserMessage
    {
        public string Message { get; set; }
    }
}
