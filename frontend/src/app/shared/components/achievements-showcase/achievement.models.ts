import type { LucideIcon } from '@lucide/angular';

export type RibbonStatId = 'ranks' | 'medals' | 'projects' | 'participations' | 'students';

export interface RibbonStat {
  id: RibbonStatId;
  targetNumber: number;
  prefix?: string;
  suffix?: string;
  label: string;
  icon: LucideIcon;
  currentDisplay: number;
}

export interface AchievementStory {
  id: string;
  badge: string;
  badgeIcon: string;
  title: string;
  summary: string;
  fullText: string;
  coverImage: string;
  images: string[];
  tags: string[];
  dateStr?: string;
}
