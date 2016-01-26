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
}
