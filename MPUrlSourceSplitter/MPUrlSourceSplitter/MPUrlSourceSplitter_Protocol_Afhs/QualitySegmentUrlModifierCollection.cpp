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

#include "QualitySegmentUrlModifierCollection.h"

CQualitySegmentUrlModifierCollection::CQualitySegmentUrlModifierCollection(void)
  : CCollection(CCollection::Delete)
{
}

CQualitySegmentUrlModifierCollection::~CQualitySegmentUrlModifierCollection(void)
{
}

int CQualitySegmentUrlModifierCollection::CompareItemKeys(wchar_t *firstKey, wchar_t *secondKey, void *context)
{
  bool invariant = (*(bool *)context);

  if (invariant)
  {
    return _wcsicmp(firstKey, secondKey);
  }
  else
  {
    return wcscmp(firstKey, secondKey);
  }
}

wchar_t *CQualitySegmentUrlModifierCollection::GetKey(CQualitySegmentUrlModifier *item)
{
  return Duplicate(item->GetQualitySegmentUrlModifier());
}

void CQualitySegmentUrlModifierCollection::FreeKey(wchar_t *key)
{
  FREE_MEM(key);
}

CQualitySegmentUrlModifier *CQualitySegmentUrlModifierCollection::Clone(CQualitySegmentUrlModifier *item)
{
  return NULL;
}