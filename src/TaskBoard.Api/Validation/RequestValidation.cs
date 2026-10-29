using System.Globalization;
using Grpc.Core;

namespace TaskBoard.Api.Validation;

// Request checks shared by the services. A plain static helper instead of a validation library:
// there are only a few rules and each one reads as a single line at the call site.
// Every failure is InvalidArgument - "this request is wrong no matter what the database holds".
internal static class RequestValidation
{
    public static void RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw InvalidArgument("name must not be empty.");
    }

    // The token is an opaque string to clients, but this server issues plain row offsets (see
    // ListTaskItems), so anything that is not a non-negative integer was never issued by us.
    // NumberStyles.None also rejects a sign and surrounding whitespace, so "-1" and " 1" fail.
    public static int ParsePageToken(string pageToken)
    {
        if (string.IsNullOrEmpty(pageToken))
            return 0;

        if (!int.TryParse(pageToken, NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
            throw InvalidArgument("page_token is not valid.");

        return offset;
    }

    private static RpcException InvalidArgument(string message) =>
        new(new Status(StatusCode.InvalidArgument, message));
}
