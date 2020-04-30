﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace TpacTool.Lib
{
	public sealed class ShapeCapsule
	{
		public float Radius { get; set; }

		public Vector3 Center { get; set; }

		public Vector3 Axis { get; set; }

		public string PhysicsMaterialName { get; set; }

		public List<string> Flags { get; private set; }

		public uint UnknownUint { get; private set; }

		public uint UnknownUint2 { get; private set; }

		public ShapeCapsule()
		{
			Center = new Vector3();
			Axis = new Vector3();
			PhysicsMaterialName = String.Empty;
			Flags = new List<string>();
		}

		public ShapeCapsule(BinaryReader stream)
		{
			UnknownUint = stream.ReadUInt32();
			Radius = stream.ReadSingle();
			Center = stream.ReadVec4AsVec3();
			Axis = stream.ReadVec4AsVec3();
			UnknownUint2 = stream.ReadUInt32();
			Flags = stream.ReadStringList();
			PhysicsMaterialName = stream.ReadSizedString();
		}
	}
}