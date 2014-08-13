using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PSUtilsLauncher
{
    public class TaskExecutionException: Exception
    {
        public TaskExecutionException(Exception exception)
            : base(exception.Message, exception)
        {
        }
    }
}
