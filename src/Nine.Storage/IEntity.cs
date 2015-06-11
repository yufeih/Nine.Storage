namespace Nine.Storage
{
    using System;
    
    /// <summary>
    /// Represents a storage object that can be saved
    /// </summary>
    public interface IKeyed
    {
        /// <summary>
        /// Gets or sets a key that identifies this object.
        /// </summary>
        string GetKey();
    }

    /// <summary>
    /// Represents a storage object that can be saved
    /// </summary>
    public interface ITimestamped
    {
        /// <summary>
        /// Gets or sets the last updated time of this storage object.
        /// </summary>
        DateTime Time { get; set; }
    }

    /// <summary>
    /// Indicates that this object is owned by a particular user
    /// </summary>
    public interface IOwned
    {
        string UserId { get; set; }
    }
}
