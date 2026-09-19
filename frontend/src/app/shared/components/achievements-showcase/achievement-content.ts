import { RibbonStat, AchievementStory } from './achievement.models';
import {
  LucideGraduationCap,
  LucideLightbulb,
  LucideMedal,
  LucideTrophy,
  LucideUsersRound
} from '@lucide/angular';

// 5 Key Stats with animated numbers
export const ACHIEVEMENT_STATS: RibbonStat[] = [
  { id: 'ranks', targetNumber: 9, label: 'LANDING.STATS.ranks', icon: LucideTrophy, currentDisplay: 0 },
  { id: 'medals', targetNumber: 26, label: 'LANDING.STATS.medals', icon: LucideMedal, currentDisplay: 0 },
  {
    id: 'projects',
    targetNumber: 19,
    label: 'LANDING.STATS.projects',
    icon: LucideLightbulb,
    currentDisplay: 0
  },
  {
    id: 'participations',
    targetNumber: 1970,
    prefix: '+',
    label: 'LANDING.STATS.participations',
    icon: LucideUsersRound,
    currentDisplay: 0
  },
  {
    id: 'students',
    targetNumber: 317,
    label: 'LANDING.STATS.students',
    icon: LucideGraduationCap,
    currentDisplay: 0
  }
];

// Unique 3 main stories
export const ACHIEVEMENT_STORIES: AchievementStory[] = [
  {
    id: 'story-1',
    badge: 'LANDING.STORIES.story-1.badge',
    badgeIcon: 'pi-verified',
    title: 'LANDING.STORIES.story-1.title',
    summary: 'LANDING.STORIES.story-1.summary',
    fullText: 'LANDING.STORIES.story-1.fullText',
    coverImage: 'assets/achievements/1/TWMate.com-826be516744c4433f559426bb2649e6f.jpg',
    images: ['assets/achievements/1/TWMate.com-826be516744c4433f559426bb2649e6f.jpg'],
    tags: [
      'LANDING.STORIES.story-1.tags.0',
      'LANDING.STORIES.story-1.tags.1',
      'LANDING.STORIES.story-1.tags.2'
    ],
    dateStr: 'LANDING.STORIES.story-1.dateStr'
  },
  {
    id: 'story-5',
    badge: 'LANDING.STORIES.story-5.badge',
    badgeIcon: 'pi-images',
    title: 'LANDING.STORIES.story-5.title',
    summary: 'LANDING.STORIES.story-5.summary',
    fullText: 'LANDING.STORIES.story-5.fullText',
    coverImage: 'assets/achievements/5/HKMiBPoXUAAfWqJ.jpg',
    images: [
      'assets/achievements/5/HKMiBPoXUAAfWqJ.jpg',
      'assets/achievements/5/HKMiBPpWAAAHXyx.jpg',
      'assets/achievements/5/HKMiBPrWAAAOUgl.jpg',
      'assets/achievements/5/HKMiBSRWwAAjhUq.jpg'
    ],
    tags: [
      'LANDING.STORIES.story-5.tags.0',
      'LANDING.STORIES.story-5.tags.1',
      'LANDING.STORIES.story-5.tags.2'
    ],
    dateStr: 'LANDING.STORIES.story-5.dateStr'
  },
  {
    id: 'story-3',
    badge: 'LANDING.STORIES.story-3.badge',
    badgeIcon: 'pi-check-circle',
    title: 'LANDING.STORIES.story-3.title',
    summary: 'LANDING.STORIES.story-3.summary',
    fullText: 'LANDING.STORIES.story-3.fullText',
    coverImage: 'assets/achievements/3/HMN3sKGWsAA4sIx.jpg',
    images: ['assets/achievements/3/HMN3sKGWsAA4sIx.jpg'],
    tags: [
      'LANDING.STORIES.story-3.tags.0',
      'LANDING.STORIES.story-3.tags.1',
      'LANDING.STORIES.story-3.tags.2'
    ],
    dateStr: 'LANDING.STORIES.story-3.dateStr'
  }
];
