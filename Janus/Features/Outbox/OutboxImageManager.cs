﻿using System.Windows.Forms;

using CodeJam.Extensibility;

namespace Rsdn.Janus
{
	// TODO : убить статику, заменить на сервис.
	/// <summary>
	/// Управляет картинками.
	/// </summary>
	public class OutboxImageManager
	{
		private static ImageList _imageList;

		private static int _msgImageIndex;
		private static int _msgWaitImageIndex;
		private static int _msgReplyImageIndex;
		private static int _msgWaitReplyImageIndex;
		private static int _msgFolderImageIndex;
		private static int _rate1ImageIndex;
		private static int _rate2ImageIndex;
		private static int _rate3ImageIndex;
		private static int _rateSmileImageIndex;
		private static int _rateDisagreeImageIndex;
		private static int _rateAgreeImageIndex;
		private static int _ratePlus1ImageIndex;
		private static int _rateDeleteImageIndex;
		private static int _rateFolderImageIndex;
		private static int _regetTopicImageIndex;
		private static int _regetTopicFolderImageIndex;

		public static ImageList ImageList
		{
			get 
			{
				if (_imageList == null)
					InitImageList();
				return _imageList; 
			}
		}

		public static int MsgImageIndex => _msgImageIndex;

		public static int MsgWaitImageIndex => _msgWaitImageIndex;

		public static int MsgReplyImageIndex => _msgReplyImageIndex;

		public static int MsgWaitReplyImageIndex => _msgWaitReplyImageIndex;

		public static int MsgFolderImageIndex => _msgFolderImageIndex;

		public static int Rate1ImageIndex => _rate1ImageIndex;

		public static int Rate2ImageIndex => _rate2ImageIndex;

		public static int Rate3ImageIndex => _rate3ImageIndex;

		public static int RateSmileImageIndex => _rateSmileImageIndex;

		public static int RateDisagreeImageIndex => _rateDisagreeImageIndex;

		public static int RateAgreeImageIndex => _rateAgreeImageIndex;

		public static int RatePlus1ImageIndex => _ratePlus1ImageIndex;

		public static int RateDeleteImageIndex => _rateDeleteImageIndex;

		public static int RateFolderImageIndex => _rateFolderImageIndex;

		public static int RegetTopicImageIndex => _regetTopicImageIndex;

		public static int RegetTopicFolderImageIndex => _regetTopicFolderImageIndex;

		private static void InitImageList()
		{
			const string prefix = @"Outbox\";

			_imageList = new ImageList {ColorDepth = ColorDepth.Depth32Bit};

			var styleImageManager =
				ApplicationManager.Instance.ServiceProvider.GetRequiredService<IStyleImageManager>();
			_msgImageIndex =
				styleImageManager.AppendImage(prefix + "Msg", StyleImageType.ConstSize, _imageList);
			_msgWaitImageIndex =
				styleImageManager.AppendImage(prefix + "MsgWait", StyleImageType.ConstSize, _imageList);
			_msgReplyImageIndex =
				styleImageManager.AppendImage(prefix + "MsgReply", StyleImageType.ConstSize, _imageList);
			_msgWaitReplyImageIndex =
				styleImageManager.AppendImage(prefix + "MsgWaitReply", StyleImageType.ConstSize, _imageList);
			_msgFolderImageIndex =
				styleImageManager.AppendImage(prefix + "MsgFolder", StyleImageType.ConstSize, _imageList);
			_rate1ImageIndex =
				styleImageManager.AppendImage(prefix + "Rate1", StyleImageType.ConstSize, _imageList);
			_rate2ImageIndex =
				styleImageManager.AppendImage(prefix + "Rate2", StyleImageType.ConstSize, _imageList);
			_rate3ImageIndex =
				styleImageManager.AppendImage(prefix + "Rate3", StyleImageType.ConstSize, _imageList);
			_rateAgreeImageIndex =
				styleImageManager.AppendImage(prefix + "Rate-4", StyleImageType.ConstSize, _imageList);
			_rateDisagreeImageIndex =
				styleImageManager.AppendImage(prefix + "Rate0", StyleImageType.ConstSize, _imageList);
			_rateSmileImageIndex =
				styleImageManager.AppendImage(prefix + "Rate-2", StyleImageType.ConstSize, _imageList);
			_ratePlus1ImageIndex =
				styleImageManager.AppendImage(prefix + "Rate-3", StyleImageType.ConstSize, _imageList);
			_rateDeleteImageIndex =
				styleImageManager.AppendImage(prefix + "Rate-1", StyleImageType.ConstSize, _imageList);
			_rateFolderImageIndex =
				styleImageManager.AppendImage(prefix + "RateFolder", StyleImageType.ConstSize, _imageList);
			_regetTopicImageIndex =
				styleImageManager.AppendImage("RegetTopic", StyleImageType.Small, _imageList);
			_regetTopicFolderImageIndex =
				styleImageManager.AppendImage(prefix + "RegetTopicFolder", StyleImageType.ConstSize, _imageList);
		}
	}
}