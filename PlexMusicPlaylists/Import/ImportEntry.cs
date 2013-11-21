﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PlexMusicPlaylists.PlexMediaServer;
using System.Windows.Forms;

namespace PlexMusicPlaylists.Import
{
  public class ImportEntry
  {
    private string m_fullFileName = "";
    private string m_fullPlexFileName = "";
    private List<MatchEntry> m_TitleMatches = new List<MatchEntry>();

    public string Artist { get; set; }
    public string Title { get; set; }
    public int Duration { get; set; }
    public string FileName { get; set; }
    public ImportFile Owner { get; set; }
    public MainSection MainSection { get; set; }
    public LibrarySection FolderSection { get; set; }
    public string MainSectionName
    {
      get
      {
        return MainSection != null ? MainSection.Title : "";
      }
    }
    public string PMSFolder
    {
      get
      {
        return FolderSection != null ? FolderSection.Title : "";
      }
    }
    public string Key { get; set; }
    public bool Matched { get { return !String.IsNullOrEmpty(Key); } }
    public List<MatchEntry> TitleMatches { get { return m_TitleMatches; } }
    public string TrackType { get; set; }
    public int MatchedOnTitleCount
    {
      get { return m_TitleMatches != null ? m_TitleMatches.Count : 0; }
    }

    public string Info
    {
      get
      {
        return String.Format("{0} - {1} [{2}]", Artist, Title, FullFileName);
      }
    }

    public static string normalizePath(string _path, char _fromDirectorySeparator, char _toDirectorySeparator)
    {
      if (!String.IsNullOrEmpty(_path) && !_fromDirectorySeparator.Equals(_toDirectorySeparator))
      {
        _path = _path.Replace(_fromDirectorySeparator, _toDirectorySeparator);
      }
      return _path;
    }

    public string FullFileName
    {
      get
      {
        if (String.IsNullOrEmpty(m_fullFileName) && !String.IsNullOrEmpty(FileName))
        {
          m_fullFileName = Path.IsPathRooted(FileName) ? Path.GetFullPath(FileName) : Path.Combine(Owner.FullPath, FileName);
          m_fullFileName = normalizePath(m_fullFileName, Owner.DirectorySeparator, PMSServer.DirectorySeparator);
        }
        return m_fullFileName ?? "";
      }
    }

    public string FullPlexFileName
    {
      get { return m_fullPlexFileName; }
    }

    public string FileNameOnly
    {
      get
      {
        return Path.GetFileName(FullFileName);
      }
    }

    public void setSectionLocation(SectionLocation _sectionLocation, string _baseUrl, ImportManager.ProgressEventHandler _progressMessage)
    {
      string relativePath = RelativePath(_sectionLocation.MappedLocation);
      MainSection = _sectionLocation.Owner();
      _progressMessage(String.Format("PlexMediaServer: loading folders from section {0}.....", MainSection.Title));
      MainSection.loadFolders(_baseUrl);
      var folders =
        from folder in MainSection.folders
        where folder.Title.Equals(relativePath, StringComparison.OrdinalIgnoreCase)
        select folder;
      FolderSection = folders.FirstOrDefault();
      m_fullPlexFileName = FullFileName.Replace(_sectionLocation.MappedLocation, _sectionLocation.PlexLocation);
    }

    private string RelativePath(string _basePath)
    {
      string relativePath = FullFileName;
      if (!String.IsNullOrEmpty(_basePath))
      {
        _basePath += PMSServer.DirectorySeparator;
        if (relativePath.StartsWith(_basePath))
        {
          relativePath = relativePath.Remove(0, _basePath.Length);
        }
      }
      string fileNameOnly = Path.GetFileName(relativePath);
      return relativePath.Remove(relativePath.Length - fileNameOnly.Length - 1);
    }

    public void resetMatches(bool _matchOnFolder)
    {
      m_TitleMatches.RemoveAll(entry => entry.MatchOnFolder == _matchOnFolder);
    }

    public bool AddMatch(MatchEntry _matchEntry)
    {
      if (_matchEntry != null)
      {
        if (_matchEntry.MatchOnFolder || _matchEntry.Title.Contains(Title))
        {
          _matchEntry.MatchOnTitle = _matchEntry.Title.Equals(this.Title, StringComparison.OrdinalIgnoreCase);
          _matchEntry.MatchOnArtist = _matchEntry.IsArtistMatch(this.Artist);
          _matchEntry.MatchOnFileName = _matchEntry.FileName.Equals(this.FileNameOnly, StringComparison.OrdinalIgnoreCase);
          MatchEntry existMatch = m_TitleMatches.FirstOrDefault(match => match.Key.Equals(_matchEntry.Key, StringComparison.OrdinalIgnoreCase));
          if (existMatch != null)
          {
            if (_matchEntry.MatchOnFolder)
            {
              existMatch.MatchOnFolder = true;
            }
            else
            {
              existMatch.MatchOnTitle = _matchEntry.MatchOnTitle;
              existMatch.MatchOnArtist = _matchEntry.MatchOnArtist;
              existMatch.MatchOnFileName = _matchEntry.MatchOnFileName;
            }
          }
          else
          {
            m_TitleMatches.Add(_matchEntry);
          }
          if (!Matched && _matchEntry.MatchOnFolder)
          {
            Key = _matchEntry.Key;
            TrackType = _matchEntry.TrackType;
          }
          return true;
        }
      }
      return false;
    }

    public void CheckBestMatch()
    {
      if (!Matched)
      {
        var matches =
          from match in m_TitleMatches.OrderByDescending(m => m.MatchFactor)
          where match.MatchOnFolder || (match.MatchOnTitle && (match.MatchOnArtist || match.MatchOnFileName))
          select match;
        MatchEntry matchEntry = matches.FirstOrDefault();
        if (matchEntry != null)
        {
          // Single exact match found => use it
          Key = matchEntry.Key;
          TrackType = matchEntry.TrackType;
        }
      }
    }

    public bool FillContextMenu(ContextMenuStrip _contextMenu)
    {
      if (_contextMenu != null && MatchedOnTitleCount > 0)
      {
        _contextMenu.Items.Clear();
        _contextMenu.Items.Add("====>  SELECT THE BEST MATCH (FROM TOP 10)  <====");
        _contextMenu.Items.Add(new ToolStripSeparator());
        foreach (MatchEntry matchEntry in m_TitleMatches.OrderByDescending(m => m.MatchFactor))
        {
          ToolStripButton toolStripItem = new ToolStripButton(matchEntry.Info);
          toolStripItem.Tag = matchEntry;
          if (matchEntry.Key.Equals(Key))
          {
            toolStripItem.Image = Properties.Resources.OKmark;
          }

          // add only top 10 hits to the menu
          if (_contextMenu.Items.Add(toolStripItem) >= 12)
            break;
        }
        
        return true;
      }
      return false;
    }

    public bool selectMatch(MatchEntry _matchEntry)
    {
      if (_matchEntry != null)
      {
        // Match selected from popup menu => use it
        Key = _matchEntry.Key;
        TrackType = _matchEntry.TrackType;
        return Matched;
      }
      return false;
    }
  }
}