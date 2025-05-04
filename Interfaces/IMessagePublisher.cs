namespace WasteCollection.Api.Interfaces;

public interface IMessagePublisher
{
    Task PublishNewRequestNotification(int requestId);
}