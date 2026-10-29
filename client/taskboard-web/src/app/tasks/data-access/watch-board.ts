import { noop, Observable } from 'rxjs';
import type { BoardServiceClient } from '../../core/proto-gen/board.client';
import type { BoardEvent } from '../../core/proto-gen/board';

// Cold Observable around one WatchBoard server-streaming call: every subscription opens a NEW call,
// which is what lets RxJS retry()/defer() reconnect by simply resubscribing.
// `onOpen` fires when the response headers arrive. The server flushes them immediately on subscribe,
// so this is the earliest moment we know the stream is really established (a message may take hours).
export function watchBoard$(client: BoardServiceClient, boardId: number, onOpen?: () => void): Observable<BoardEvent> {
  return new Observable<BoardEvent>((subscriber) => {
    // A fetch() based gRPC-Web call can only be cancelled through an AbortSignal.
    const controller = new AbortController();
    const call = client.watchBoard({ boardId }, { abort: controller.signal });

    // The error branch is a no-op on purpose: failures are reported once, through `responses.onError`.
    // The closed check stops a promise callback that was already queued from reporting "open" after teardown.
    call.headers.then(() => !subscriber.closed && onOpen?.(), noop);

    const removeListeners = [
      call.responses.onMessage((event) => subscriber.next(event)),
      call.responses.onError((error) => subscriber.error(error)),
      call.responses.onComplete(() => subscriber.complete())
    ];

    return () => {
      // Detach first so the CANCELLED error produced by our own abort is not delivered anywhere.
      removeListeners.forEach((remove) => remove());
      controller.abort();
    };
  });
}
