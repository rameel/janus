﻿namespace Rsdn.Janus
{
	public interface IUserForumMessagesStats
	{
		int ForumId { get; }
		string ForumName { get; }
		string ForumDescription { get; }
		bool ForumInTop { get; }
		bool IsForumSubscribed { get; }
		int MessagesCount { get; }
	}
}
