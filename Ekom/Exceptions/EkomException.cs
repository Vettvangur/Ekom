namespace Ekom.Exceptions
{
    /// <summary>
    /// Common base type for all exceptions thrown by Ekom
    /// </summary>
    public class EkomException : Exception
    {
        public EkomException()
        {

        }
        public EkomException(string message) : base(message)
        {
        }

        public EkomException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
