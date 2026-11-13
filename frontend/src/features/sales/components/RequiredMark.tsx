/** Dấu `*` đỏ sau nhãn của ô bắt buộc. */
export function RequiredMark() {
  return (
    <span aria-hidden className="text-destructive">
      *
    </span>
  );
}
