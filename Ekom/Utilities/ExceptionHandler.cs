using Ekom.Exceptions;
using System;
using System.Net;

namespace Ekom.Utilities
{
    static class ExceptionHandler
    {
        /// <summary>
        /// Standardizes exception handling in Ekom Controllers for the most common exception types.
        /// </summary>
        /// <typeparam name="T">Only ActionResult and HttpResponseMessage are likely to make sense here</typeparam>
        /// <returns></returns>
        public static T Handle<T>(
            Exception exception,
            Func<T> defaultHandler = null,
            Func<T> preHandler = null) where T : class
        {
            //if (!typeof(ActionResult).IsAssignableFrom(typeof(T)) && typeof(T) != typeof(HttpResponseMessage))
            //{
            //    throw new ArgumentException("Only ActionResult or HttpResponseMessage are supported as the generic return type");
            //}

            if (preHandler != null)
            {
                var actionResult = preHandler();
                if (actionResult != null)
                {
                    return actionResult;
                }
            }

            if (exception is OrderLineNegativeException)
            {
                return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.BadRequest);
            }
            // Missing parameters should be handled by the controller, these exceptions are therefore likely
            // InternalServerError's
            //else if (exception is ArgumentException)
            //{
            //    return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.BadRequest);
            //}
            //else if (exception is ArgumentNullException)
            //{
            //    return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.BadRequest);
            //}
            else if (exception is OrderLineNotFoundException)
            {
                return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.NotFound);
            }
            else if (exception is ProductNotFoundException)
            {
                return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.NotFound);
            }
            else if (exception is VariantNotFoundException)
            {
                return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.NotFound);
            }
            else if (exception is NotEnoughStockException)
            {
                return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.Conflict);
            }

            if (defaultHandler != null)
            {
                var actionResult = defaultHandler();
                if (actionResult != null)
                {
                    return actionResult;
                }
            }
            return null;
        }

        /// <summary>
        /// Standardizes exception handling in Ekom Controllers for the most common exception types.
        /// </summary>
        /// <typeparam name="T">Only ActionResult, HttpResponseException and HttpResponseMessage are likely to make sense here</typeparam>
        /// <returns></returns>
        //public static T TryCatch<T>(
        //    Exception exception,
        //    Func<T> defaultHandler = null,
        //    Func<T> preHandler = null) where T : class
        //{
        //    //if (!typeof(ActionResult).IsAssignableFrom(typeof(T)) && typeof(T) != typeof(HttpResponseMessage))
        //    //{
        //    //    throw new ArgumentException("Only ActionResult or HttpResponseMessage are supported as the generic return type");
        //    //}

        //    if (preHandler != null)
        //    {
        //        var actionResult = preHandler();
        //        if (actionResult != null)
        //        {
        //            return actionResult;
        //        }
        //    }

        //    if (exception is OrderLineNegativeException)
        //    {
        //        if (typeof(T) == typeof(IActionResult))
        //        {
        //            throw new HttpResponseException(HttpStatusCode.BadRequest);
        //        }
        //        return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.BadRequest);
        //    }
        //    // Missing parameters should be handled by the controller, these exceptions are therefore likely
        //    // InternalServerError's
        //    //else if (exception is ArgumentException)
        //    //{
        //    //    return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.BadRequest);
        //    //}
        //    //else if (exception is ArgumentNullException)
        //    //{
        //    //    return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.BadRequest);
        //    //}
        //    else if (exception is OrderLineNotFoundException)
        //    {
        //        if (typeof(T) == typeof(IActionResult))
        //        {
        //            throw new HttpResponseException(HttpStatusCode.NotFound);
        //        }

        //        return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.NotFound);
        //    }
        //    else if (exception is ProductNotFoundException)
        //    {
        //        if (typeof(T) == typeof(IActionResult))
        //        {
        //            throw new HttpResponseException(HttpStatusCode.NotFound);
        //        }

        //        return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.NotFound);
        //    }
        //    else if (exception is DiscountNotFoundException)
        //    {
        //        if (typeof(T) == typeof(IActionResult))
        //        {
        //            throw new HttpResponseException(HttpStatusCode.NotFound);
        //        }

        //        return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.NotFound);
        //    }
        //    else if (exception is VariantNotFoundException)
        //    {
        //        if (typeof(T) == typeof(IActionResult))
        //        {
        //            throw new HttpResponseException(HttpStatusCode.NotFound);
        //        }

        //        return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.NotFound);
        //    }
        //    else if (exception is NotEnoughStockException)
        //    {
        //        if (typeof(T) == typeof(IActionResult))
        //        {
        //            throw new HttpResponseException(HttpStatusCode.Conflict);
        //        }

        //        return (T)Activator.CreateInstance(typeof(T), HttpStatusCode.Conflict);
        //    }

        //    if (defaultHandler != null)
        //    {
        //        var actionResult = defaultHandler();
        //        if (actionResult != null)
        //        {
        //            return actionResult;
        //        }
        //    }
        //    return null;
        //}
    }
}
