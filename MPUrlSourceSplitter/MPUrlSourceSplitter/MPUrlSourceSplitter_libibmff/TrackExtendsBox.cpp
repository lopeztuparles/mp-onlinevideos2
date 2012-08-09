/*
    Copyright (C) 2007-2010 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2.  If not, see <http://www.gnu.org/licenses/>.
*/

#include "StdAfx.h"

#include "TrackExtendsBox.h"

CTrackExtendsBox::CTrackExtendsBox(void)
  : CFullBox()
{
  this->type = Duplicate(TRACK_EXTENDS_BOX_TYPE);
  this->trackId = 0;
  this->defaultSampleDescriptionIndex = 0;
  this->defaultSampleDuration = 0;
  this->defaultSampleFlags = 0;
  this->defaultSampleSize = 0;
}

CTrackExtendsBox::~CTrackExtendsBox(void)
{
}

/* get methods */

bool CTrackExtendsBox::GetBox(uint8_t **buffer, uint32_t *length)
{
  bool result = __super::GetBox(buffer, length);

  if (result)
  {
    uint32_t position = this->HasExtendedHeader() ? BOX_HEADER_LENGTH_SIZE64 : BOX_HEADER_LENGTH;

    if (!result)
    {
      FREE_MEM(*buffer);
      *length = 0;
    }
  }

  return result;
}

uint32_t CTrackExtendsBox::GetTrackId(void)
{
  return this->trackId;
}

uint32_t CTrackExtendsBox::GetDefaultSampleDescriptionIndex(void)
{
  return this->defaultSampleDescriptionIndex;
}

uint32_t CTrackExtendsBox::GetDefaultSampleDuration(void)
{
  return this->defaultSampleDuration;
}

uint32_t CTrackExtendsBox::GetDefaultSampleSize(void)
{
  return this->defaultSampleSize;
}

uint32_t CTrackExtendsBox::GetDefaultSampleFlags(void)
{
  return this->defaultSampleFlags;
}

/* set methods */

/* other methods */

bool CTrackExtendsBox::Parse(const uint8_t *buffer, uint32_t length)
{
  return this->ParseInternal(buffer, length, true);
}

wchar_t *CTrackExtendsBox::GetParsedHumanReadable(const wchar_t *indent)
{
  wchar_t *result = NULL;
  wchar_t *previousResult = __super::GetParsedHumanReadable(indent);

  if ((previousResult != NULL) && (this->IsParsed()))
  {
    // prepare finally human readable representation
    result = FormatString(
      L"%s\n" \
      L"%sTrack ID: %u\n" \
      L"%sDefault sample description index: %u\n" \
      L"%sDefault sample duration: %u\n" \
      L"%sDefault sample size: %u\n" \
      L"%sDefault sample flags: 0x%08X"
      ,
      
      previousResult,
      indent, this->GetTrackId(),
      indent, this->GetDefaultSampleDescriptionIndex(),
      indent, this->GetDefaultSampleDuration(),
      indent, this->GetDefaultSampleSize(),
      indent, this->GetDefaultSampleFlags()
      );
  }

  FREE_MEM(previousResult);

  return result;
}

uint64_t CTrackExtendsBox::GetBoxSize(uint64_t size)
{
  return __super::GetBoxSize(size);
}

bool CTrackExtendsBox::ParseInternal(const unsigned char *buffer, uint32_t length, bool processAdditionalBoxes)
{
  this->defaultSampleDescriptionIndex = 0;
  this->defaultSampleDuration = 0;
  this->defaultSampleFlags = 0;
  this->defaultSampleSize = 0;
  this->trackId = 0;

  bool result = __super::ParseInternal(buffer, length, false);

  if (result)
  {
    if (wcscmp(this->type, TRACK_EXTENDS_BOX_TYPE) != 0)
    {
      // incorect box type
      this->parsed = false;
    }
    else
    {
      // box is file type box, parse all values
      uint32_t position = this->HasExtendedHeader() ? FULL_BOX_HEADER_LENGTH_SIZE64 : FULL_BOX_HEADER_LENGTH;
      bool continueParsing = (this->GetSize() <= (uint64_t)length);

      if (continueParsing)
      {
        RBE32INC(buffer, position, this->trackId);
        RBE32INC(buffer, position, this->defaultSampleDescriptionIndex);
        RBE32INC(buffer, position, this->defaultSampleDuration);
        RBE32INC(buffer, position, this->defaultSampleSize);
        RBE32INC(buffer, position, this->defaultSampleFlags);
      }

      if (continueParsing && processAdditionalBoxes)
      {
        this->ProcessAdditionalBoxes(buffer, length, position);
      }

      this->parsed = continueParsing;
    }
  }

  result = this->parsed;

  return result;
}