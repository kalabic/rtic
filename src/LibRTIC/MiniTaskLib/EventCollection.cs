using LibRTIC.MiniTaskLib.Base;
using LibRTIC.MiniTaskLib.Model;
using System;
using System.Collections.ObjectModel;

namespace LibRTIC.MiniTaskLib;

/// <summary>
/// All events in _collection are defined by C# generics. They need to be enabled first using
/// <see cref="EnableInvokeFor"/> before <see cref="Invoke"/> of an event handler will work.
/// </summary>
public class EventCollection : IDisposable
{
#if DEBUG_UNDISPOSED
    public static int UNDISPOSED_COUNT = 0;

    public static int INSTANCE_COUNT = 0;

    private bool _disposed = false;
#endif

    public string Label = "";

    public bool IsComplete { get { return _complete; } }

    private object _lock = new object();

    private Info _info;

    private bool _complete = false;

    private Collection<IDisposable> _eventConnections = new();

    private Collection<IEventContainer> _collection = new();

    public EventCollection(Info info, string label)
    {
        this._info = info;
        this.Label = label;
#if DEBUG_UNDISPOSED
        Interlocked.Increment(ref UNDISPOSED_COUNT);
        Interlocked.Increment(ref INSTANCE_COUNT);
#endif
    }

#if DEBUG_UNDISPOSED
    ~EventCollection()
    {
        Dispose(false);
        Interlocked.Decrement(ref INSTANCE_COUNT);
    }
#endif

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
#if DEBUG_UNDISPOSED
        if (!_disposed && !disposing)
        {
            throw new InvalidOperationException("Not disposed properly.");
        }
        if (!_disposed)
        {
            _disposed = true;
            Interlocked.Decrement(ref UNDISPOSED_COUNT);
        }
#endif

        // Release managed resources.
        if (disposing)
        {
            Clear();
        }
        // Release unmanaged resources.
    }

    public virtual void Clear()
    {
        lock (_lock)
        {
            _complete = true;
            foreach (IDisposable connection in _eventConnections)
            {
                connection.Dispose();
            }
            _eventConnections.Clear();
            _collection.Clear();
        }
    }

    /// <summary>
    /// For 'Invoke' and 'Connect' to work for specific type of object (TMessage), it is first necessary to enable them.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <exception cref="ArgumentException"></exception>
    public EventContainer<TMessage>? EnableInvokeFor<TMessage>(bool assureIsNewEvent = false)
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventCollection.");
#else
                return null;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            if (items.Count() == 0)
            {
                var item = new EventContainer<TMessage>(_info);
                _collection.Add(item);
                return item;
            }
            else if (assureIsNewEvent)
            {
                throw new ArgumentException("Attempted to add pre-existing event type into EventSourceCollection.");
            }
            else
            {
                return items.First();
            }
        }
    }

    public void MakeCompatible(EventCollection other)
    {
        foreach (var otherItem in other._collection)
        {
            bool exists = false;
            foreach (var item in _collection)
            {
                if (item.GetType() == otherItem.GetType())
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                _collection.Add(otherItem.NewCompatibleInstance());
            }
        }
    }

    public bool Exists<TMessage>()
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventCollection.");
#else
                return false;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            return items.Count() > 0;
        }
    }

    public void Invoke<TMessage>(TMessage update, object? sender = null)
    {
        var item = GetEventContainer<TMessage>();
        item?.Invoke(sender, update);
    }

    public void HandleInvoke<TMessage>(object? sender, TMessage update)
    {
        var item = GetEventContainer<TMessage>();
        item?.Invoke(sender, update);
    }

    public EventContainer<TMessage>? GetEventContainer<TMessage>()
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventCollection.");
#else
                return null;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            if (items.Count() > 1)
            {
                throw new ArgumentException("Attempted to access duplicated event type in EventSourceCollection.");
            }
            else if (items.Count() == 1)
            {
                return items.First();
            }

#if DEBUG
            throw new ArgumentException("Attempted to access non-existent event type in EventSourceCollection.");
#else
            return null;
#endif
        }
    }

    public EventForwarder<TMessage>? NewEventForwarder<TMessage>(IQueueWriter<IProcessMessage> destinationQueue)
    {
        var item = GetEventContainer<TMessage>();
        if (item is not null)
        {
            return new EventForwarder<TMessage>(_info, destinationQueue, item);
        }
        else
        {
            return null;
        }
    }

    public EventContainer<TMessage>? ForwardFrom<TMessage>(EventCollection otherCollection, IQueueWriter<IProcessMessage> destinationQueue)
    {
        if (!Exists<TMessage>())
        {
            EnableInvokeFor<TMessage>();
        }

        if (!otherCollection.Exists<TMessage>())
        {
            otherCollection.EnableInvokeFor<TMessage>();
        }

        var forwarder = NewEventForwarder<TMessage>(destinationQueue);
        if (forwarder is not null)
        {
            return otherCollection.Connect<TMessage>(forwarder);
        }

        return null;
    }

    public void Connect<TMessage>(EventCollection eventCollection)
    {
        var item = GetEventContainer<TMessage>();
        EventContainer<TMessage>? other = eventCollection.GetEventContainer<TMessage>();
        if (item is not null && other is not null)
        {
            lock (_lock)
            {
                ConnectEventHandler(item, other);
            }
        }
    }

    public void Connect<TMessage>(EventHandler<TMessage> eventHandler)
    {
        Connect(false, eventHandler);
    }

    public void Connect<TMessage>(bool assertEventExists, EventHandler<TMessage> eventHandler)
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventCollection.");
#else
                return;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            if (items.Count() > 1)
            {
                throw new ArgumentException("Attempted to access duplicated event type in EventSourceCollection.");
            }
            else if (items.Count() == 1)
            {
                ConnectEventHandler(items.First(), eventHandler);
            }
            else
            {
                if (assertEventExists)
                {
                    throw new ArgumentException("Attempted to access non-existent event type in EventSourceCollection.");
                }
                else
                {
                    var item = EnableInvokeFor<TMessage>();
                    if (item is not null)
                    {
                        ConnectEventHandler(item, eventHandler);
                    }
                }
            }
        }
    }

    public void ConnectAsync<TMessage>(EventHandler<TMessage> eventHandler)
    {
        ConnectAsync(true, eventHandler);
    }

    public void ConnectAsync<TMessage>(bool assertEventExists, EventHandler<TMessage> eventHandler)
    {
        var asyncEventHandler = new EventHandler<TMessage>(
            (sender, message) =>  Task.Run(() => eventHandler.Invoke(sender, message)));

        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventCollection.");
#else
                return;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            if (items.Count() > 1)
            {
                throw new ArgumentException("Attempted to access duplicated event type in EventSourceCollection.");
            }
            else if (items.Count() == 1)
            {
                ConnectEventHandlerAsync(items.First(), asyncEventHandler);
            }
            else
            {
                if (assertEventExists)
                {
                    throw new ArgumentException("Attempted to access non-existent event type in EventSourceCollection.");
                }
                else
                {
                    var item = EnableInvokeFor<TMessage>();
                    if (item is not null)
                    {
                        ConnectEventHandlerAsync(item, asyncEventHandler);
                    }
                }
            }
        }
    }

    public EventContainer<TMessage> Connect<TMessage>(EventForwarder<TMessage> forwarder)
    {
        lock (_lock)
        {
            if (_complete)
            {
#if DEBUG
                throw new ArgumentException("Access forbidden into completed EventCollection.");
#else
                return null;
#endif
            }

            var items = _collection.OfType<EventContainer<TMessage>>();
            if (items.Count() > 1)
            {
                throw new ArgumentException("Attempted to access duplicated event type in EventSourceCollection.");
            }
            else if (items.Count() == 1)
            {
                var item = items.First();
                ConnectEventForwarder(item, forwarder);
                return item;
            }
            else
            {
                throw new ArgumentException("Attempted to access non-existent event type in EventSourceCollection.");
            }
        }
    }

    private void ConnectEventHandlerAsync<TMessage>(EventContainer<TMessage> item, EventHandler<TMessage> eventHandler)
    {
        item.ConnectEventHandlerAsync(eventHandler);
        _eventConnections.Add(new EventAsyncConnection<TMessage>(item, eventHandler));
    }

    private void ConnectEventHandler<TMessage>(EventContainer<TMessage> item, EventHandler<TMessage> eventHandler)
    {
        item.ConnectEventHandler(eventHandler);
        _eventConnections.Add(new EventHandlerConnection<TMessage>(item, eventHandler));
    }

    private void ConnectEventForwarder<TMessage>(EventContainer<TMessage> item, EventForwarder<TMessage> eventForwarder)
    {
        item.ConnectEventHandler(eventForwarder.WriteToEventQueue);
        _eventConnections.Add(new EventHandlerConnection<TMessage>(item, eventForwarder.WriteToEventQueue));
        _eventConnections.Add(eventForwarder);
    }

    private void ConnectEventHandler<TMessage>(EventContainer<TMessage> item, EventContainer<TMessage> eventHandler)
    {
        item.ConnectEventHandler(eventHandler);
        _eventConnections.Add(new EventContainerConnection<TMessage>(item, eventHandler));
    }
}
