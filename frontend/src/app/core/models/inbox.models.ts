import { ConversationMessageDto } from './phase5.models';

export interface MessageWindow {
  readonly items: readonly ConversationMessageDto[];
  readonly hasOlder: boolean;
  readonly nextBeforeCursor: string | null;
}
