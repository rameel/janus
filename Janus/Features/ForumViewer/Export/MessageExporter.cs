﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using CodeJam;
using CodeJam.Services;
using CodeJam.Strings;

using Rsdn.Framework.Formatting;

using Rsdn.Janus.Framework.Imaging;

namespace Rsdn.Janus
{
	/// <summary>
	/// Экспортирует сообщения.
	/// </summary>
	public static class MessageExporter
	{
		#region Private members
		private static readonly string _exportMessageText =
			SR.Forum.ExportMessages.ExportMessage;

		private static readonly string _exportPrepareText =
			SR.Forum.ExportMessages.ExportPrepareMessage;

		private delegate void ProgressDelegate(int count, int total);
		#endregion

		#region Показ диалога и сбор сообщений
		// TODO: Весь связанный с UI код вынести в отдельный класс
		public static void Export(IServiceProvider provider)
		{
			var activeMessagesSvc = provider.GetService<IActiveMessagesService>();
			var canExportMessages = activeMessagesSvc != null && activeMessagesSvc.ActiveMessages.Any();

			var activeForumSvc = provider.GetService<IActiveForumService>();
			var canExportForum = activeForumSvc?.ActiveForum != null;

			var mainWnd = provider.GetRequiredService<IUIShell>().GetMainWindowParent();
			using (var emd = new ExportMessageDialog(canExportMessages, canExportForum))
			{
				if (emd.ShowDialog(mainWnd) != DialogResult.OK)
					return;

				var uiInfo = new {emd.FileName, emd.ExportMode, emd.ExportFormat, emd.UnreadMessagesOnly};

				var noMessages = false;
				ProgressWorker.Run(
					provider,
					false,
					pi =>
					{
						pi.SetProgressText(_exportPrepareText);

						var activeMsgSvc = provider.GetRequiredService<IActiveMessagesService>();

						// Prepare msg list
						IList<IMsg> messages;

						switch (uiInfo.ExportMode)
						{
							case ExportMode.Messages:
								messages = new List<IMsg>(
									(!uiInfo.UnreadMessagesOnly
										? activeMsgSvc.ActiveMessages
										: activeMsgSvc.ActiveMessages.Where(msg => !msg.IsRead))
										.Cast<IMsg>());
								break;

							case ExportMode.Topics:
								messages = new List<IMsg>(100);
								foreach (var msg in activeMsgSvc.ActiveMessages)
									GetAllChildren((IMsg)msg.Topic, messages, uiInfo.UnreadMessagesOnly);
								break;

							case ExportMode.Forum:
								messages = new List<IMsg>(1000);
								foreach (IMsg msg in Forums.Instance.ActiveForum.Msgs)
									GetAllChildren(msg, messages, uiInfo.UnreadMessagesOnly);
								break;

							default:
								throw new ArgumentException("Unknown export mode");
						}
						if (messages.Count == 0)
						{
							noMessages = true;
							return;
						}

						ProgressDelegate pd =
							(count, total) =>
							{
								pi.SetProgressText(_exportMessageText.FormatWith(count, total));
								pi.ReportProgress(total, count);
							};

						using (var fs = new FileStream(uiInfo.FileName, FileMode.Create))
							switch (uiInfo.ExportFormat)
							{
								case ExportFormat.Text:
									Export2Text(messages, fs, pd);
									break;
								case ExportFormat.HTML:
									Export2HTML(provider, messages, fs, pd);
									break;
								case ExportFormat.MHT:
									Export2Mht(provider, messages, fs, pd);
									break;
							}
					});
				if (noMessages)
					MessageBox.Show(mainWnd, ExportResources.NoMessages);
			}
		}

		private static void GetAllChildren(IMsg msg, ICollection<IMsg> list, bool onlyUnread)
		{
			if (!onlyUnread || msg.IsUnread)
				list.Add(msg);

			foreach (IMsg child in msg)
				GetAllChildren(child, list, onlyUnread);
		}
		#endregion

		#region Вспомогательные ф-ии
		/// <summary>
		/// Преобразовывает userclass число в строку с подсветкой или без.
		/// </summary>
		private static string FormatUserClass(UserClass userClass, bool isHtml)
		{
			return JanusFormatMessage.FormatUserClass(userClass, isHtml);
		}
		#endregion

		#region Экспорт в текст
		private static void Export2Text(ICollection<IMsg> msgs, Stream fs, ProgressDelegate pd)
		{
			var sb = new StringBuilder();
			var maxCharacters = Config.Instance.ForumExportConfig.CharsPerLine;

			var i = 0;
			foreach (var msg in msgs)
			{
				sb.Append(BuildTxtString("_", ' ', ' ', maxCharacters));
				sb.Append(BuildTxtString(SR.TGColumnDate
					+ ": " + msg.Date, '|', '|', maxCharacters));
				sb.Append(BuildTxtString(SR.TGColumnAuthor
					+ ": " + msg.UserNick + " " + FormatUserClass((UserClass)msg.UserClass, false), '|', '|',
					maxCharacters));
				sb.Append(BuildTxtString(SR.TGColumnSubject
					+ ": " + msg.Subject, '|', '|', maxCharacters));
				if (msg.GetFormattedRating().Length > 0)
					sb.Append(BuildTxtString(SR.TGColumnRate
						+ ": " + msg.GetFormattedRating(), '|', '|', maxCharacters));
				sb.Append(BuildTxtString("~", '|', '|', maxCharacters));
				sb.Append(
					BuildTxtString(
						(Config.Instance.ForumExportConfig.ExportRemoveTags ? RemoveTags(msg.Body) : msg.Body), '|',
						'|', maxCharacters));
				sb.Append(BuildTxtString("_", '|', '|', maxCharacters));
				sb.Append(BuildTxtString(" ", ' ', ' ', maxCharacters));

				i++;
				pd(i, msgs.Count);
			}

			var encoding = Encoding.Default;
			switch (Config.Instance.ForumExportConfig.ExportPlainTextEncoding)
			{
				case ExportPlainTextEncoding.Default:
					encoding = Encoding.Default;
					break;
				case ExportPlainTextEncoding.DOSCP866:
					encoding = Encoding.GetEncoding(866);
					break;
				case ExportPlainTextEncoding.Unicode:
					encoding = Encoding.Unicode;
					break;
				case ExportPlainTextEncoding.UTF8:
					encoding = Encoding.UTF8;
					break;
				case ExportPlainTextEncoding.WinCP1251:
					encoding = Encoding.GetEncoding(1251);
					break;
			}

			using (var sw = new StreamWriter(fs, encoding))
			{
				sw.Write(sb);
				sw.Flush();
			}
		}

		#region Окантовка текста в строки
		/// <summary>
		/// Делает из строки любой длины >= 1 строки, длиной не более maxLength.
		/// </summary>
		private static string BuildTxtString(string sourceString, char leftChar, char rightChar,
			int maxLength)
		{
			var charInLine = 0;
			var strLength = maxLength - 2;
			var result = new StringBuilder();

			result.Append(leftChar);

			if (sourceString.Length == 1)
				result.Append(new string(sourceString[0], strLength));
			else
				for (var pos = 0; pos < sourceString.Length; pos++)
					if (sourceString[pos] != '\r' && sourceString[pos] != '\t')
					{
						if (pos == sourceString.Length - 1 && sourceString[pos] != '\n')
						{
							result.Append(sourceString[pos]);
							charInLine++;
						}
						if (sourceString[pos] == '\n' || pos == sourceString.Length - 1)
						{
							for (var i = 0; i < strLength - charInLine; i++)
								result.Append(" ");
							charInLine = strLength;
						}
						else
						{
							result.Append(sourceString[pos]);
							charInLine++;
						}
						if (charInLine == strLength && pos != sourceString.Length - 1)
						{
							result.Append(rightChar + "\n" + leftChar);
							charInLine = 0;
						}
					}

			result.Append(rightChar + "\n");
			return result.ToString();
		}
		#endregion

		#region Срез тэгов
		/// <summary>
		/// Срез или замена [] [/] тегов.
		/// </summary>
		private static string RemoveTags(string sourceString)
		{
			sourceString = RemoveTag(sourceString, "tagline", "");
			sourceString = RemoveTag(sourceString, "q", "--===--");
			sourceString = RemoveTag(sourceString, "img", "");
			sourceString = RemoveTag(sourceString, "c#", "");
			sourceString = RemoveTag(sourceString, "nemerle", "");
			sourceString = RemoveTag(sourceString, "b", "_");
			sourceString = RemoveTag(sourceString, "i", "~");
			sourceString = RemoveTag(sourceString, "msil", "");
			sourceString = RemoveTag(sourceString, "list", "");
			sourceString = RemoveTag(sourceString, "*", "");
			sourceString = RemoveTag(sourceString, "midl", "");
			sourceString = RemoveTag(sourceString, "asm", "-<>-");
			sourceString = RemoveTag(sourceString, "ccode", "-<>-");
			sourceString = RemoveTag(sourceString, "code", "-<>-");
			sourceString = RemoveTag(sourceString, "pascal", "-<>-");
			sourceString = RemoveTag(sourceString, "vb", "-<>-");
			sourceString = RemoveTag(sourceString, "sql", "-<>-");
			sourceString = RemoveTag(sourceString, "java", "-<>-");
			sourceString = RemoveTag(sourceString, "perl", "-<>-");
			sourceString = RemoveTag(sourceString, "email", "");
			sourceString = RemoveTag(sourceString, "msdn", "");
			sourceString = RemoveTag(sourceString, "php", "-<>-");
			sourceString = RemoveTag(sourceString, "hr", "-----===-----");
			sourceString = RemoveTag(sourceString, "moderator", "!!!");

			return sourceString;
		}

		//если есть тут чтото типа #define RemoveTag(SourceString, Tag, StandOut) SourceString = SourceString.Replace("["Tag"]",StandOut); SourceString = SourceString.Replace("[/"Tag"]",StandOut);
		//то думаю лучче так сделать
		private static string RemoveTag(string sourceString, string tag, string standOut)
		{
			sourceString = sourceString.Replace("[" + tag + "]", standOut);
			sourceString = sourceString.Replace("[/" + tag + "]", standOut);

			return sourceString;
		}
		#endregion

		#endregion

		#region Экспорт в html

		#region Собственно в html
		private static void Export2HTML(IServiceProvider provider, IList<IMsg> msgs, Stream fs, ProgressDelegate pd)
		{
			using (var sw = new StreamWriter(fs, Encoding.Default))
				sw.Write(BuildHTMLPage(provider, msgs, pd, false, Encoding.UTF8));
		}
		#endregion

		#region В mht "архив"
		private const string _mhtHeader =
			@"From: <RSDN@Home>
Subject: RSDN@Home
Date: {0}
MIME-Version: 1.0
Content-Type: multipart/related; boundary=""----=_NextPart_""
X-MimeOLE: Produced By RSDN@Home Message Exporter

This is a multi-part message in MIME format.

------=_NextPart_
Content-Type: text/html; charset=""{1}""
Content-Transfer-Encoding: 8-bit

";

		// Прим. Content-Location настроен на папку картинок стандартного форматтера.
		private const string _mhtContentImageHeader =
			@"

------=_NextPart_
Content-Type: {0}; name={1}.{2}
Content-Location: /Forum/Images/{1}.{2}
Content-Transfer-Encoding: Base64

";

		private const string _mhtFooter =
			@"

------=_NextPart_--
";

		private static void Export2Mht(
			IServiceProvider provider,
			IList<IMsg> msgs,
			Stream fs,
			ProgressDelegate pd)
		{
			var encoding = Encoding.UTF8;
			using (var sw = new StreamWriter(fs, Encoding.Default))
			{
				sw.Write(
					_mhtHeader,
					DateTime.Now.ToString("ddd, d MMM yyyy h:m:s zz00", CultureInfo.InvariantCulture),
					encoding.HeaderName);
				sw.Flush();

				var htmlText = BuildHTMLPage(provider, msgs, pd, true, Encoding.UTF8);
				var page = encoding.GetBytes(htmlText);
				fs.Write(page, 0, page.Length);

				var usedSmileFiles =
					msgs
						.SelectMany(msg => TextFormatter.GetSmileFiles(msg.Body))
						.Distinct();

				const string prefix = @"ForumImages\";

				foreach (var smileName in usedSmileFiles)
				{
					var smileImage =
						provider
							.GetRequiredService<IStyleImageManager>()
							.GetImage(prefix + smileName, StyleImageType.ConstSize);
					var ifi = ImageFormatInfo.FromImageFormat(smileImage.RawFormat);

					sw.Write(_mhtContentImageHeader, ifi.MimeType, smileName, ifi.Extension);

					using (var ms = new MemoryStream())
					{
						smileImage.Save(ms, smileImage.RawFormat);
						sw.Write(Convert.ToBase64String(ms.ToArray(), Base64FormattingOptions.InsertLineBreaks));
					}
				}

				sw.Write(_mhtFooter);
			}
		}
		#endregion

		#region Создание страницы
		private const string _exportForumResource = _resourcePrefix + "forum.css";
		private const string _exportPageResource = _resourcePrefix + "page.html";
		private const string _messageFormatResource = _resourcePrefix + "MessageFormat.html";
		private const string _resourcePrefix = "Rsdn.Janus.Features.ForumViewer.Export.";

		private static string BuildHTMLPage(
			IServiceProvider provider,
			IList<IMsg> msgs,
			ProgressDelegate pd,
			bool processSmiles,
			Encoding encoding)
		{
			var formatter = new TextFormatter();
			var sb = new StringBuilder();
			var forum = new Forum(provider);
			forum.LoadData(msgs[0].ForumID);
			sb.AppendFormat(
				@"<tr><td class='s' colspan='2' align='center'>{0}&nbsp;<font size='1'>[{1}]</font></td></tr>",
				forum.Description, forum.Name);

			string messageFormat;
			using (var rd = new StreamReader(Assembly.GetExecutingAssembly().GetRequiredResourceStream(_messageFormatResource)))
				messageFormat = rd.ReadToEnd();

			var i = 0;
			foreach (var msg in msgs)
			{
				var formattedRating = msg.GetFormattedRating();

				sb.AppendFormat(
					messageFormat,
					msg.ID,
					msg.Subject,
					msg.ParentID,
					SR.Forum.ExportMessages.Export2ParentLink,
					msg.UserNick,
					FormatUserClass((UserClass)msg.UserClass, true),
					msg.Date,
					string.IsNullOrEmpty(formattedRating)
						? string.Empty
						: SR.TGColumnRate + " " + formattedRating,
					formatter.Format(msg.Body, processSmiles));

				i++;
				pd(i, msgs.Count);
			}

			string exportPageFormat;
			using (var rd = new StreamReader(Assembly.GetExecutingAssembly().GetRequiredResourceStream(_exportPageResource)))
				exportPageFormat = rd.ReadToEnd();

			string exportForumStyle;
			using (var rd = new StreamReader(Assembly.GetExecutingAssembly().GetRequiredResourceStream(_exportForumResource)))
				exportForumStyle = rd.ReadToEnd();

			return
				string.Format(
					exportPageFormat,
					forum.Description + " [" + forum.Name + "]",
					encoding.HeaderName,
					exportForumStyle,
					sb);
		}
		#endregion

		#endregion
	}
}