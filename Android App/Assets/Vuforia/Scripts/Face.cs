using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FaceCollection
{
	public List<Face> faces;
}


/// <summary>
/// The detected face entity.
/// </summary>
[Serializable]
public class Face
{
	
    /// <summary>
    /// Gets or sets the face identifier.
    /// </summary>
    /// <value>
    /// The face identifier.
    /// </value>
	public string faceId;

    /// <summary>
    /// Gets or sets the face rectangle.
    /// </summary>
    /// <value>
    /// The face rectangle.
    /// </value>
	public FaceRectangle faceRectangle;


    /// <summary>
    /// Gets or sets the face attributes.
    /// </summary>
    /// <value>
    /// The face attributes.
    /// </value>
	public FaceAttributes faceAttributes;

}
