import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { createMemoryRouter, Link, RouterProvider, useParams } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ActionReceipt, ResponsiveMasterDetail, UnsavedChangesGuard } from './WorkspacePrimitives';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('workspace navigation primitives', () => {
  it('moves focus to the selected detail heading', async () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => undefined);
    const router = createMemoryRouter([
      { path: '/items/:itemId', element: <FocusedDetail /> },
    ], { initialEntries: ['/items/one'] });

    render(<RouterProvider router={router} />);

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Карточка one' })).toHaveFocus());
    expect(scrollTo).toHaveBeenCalledWith({ top: 0, left: 0, behavior: 'auto' });
  });

  it('keeps unsaved text on screen when navigation is declined', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const router = createMemoryRouter([
      { path: '/edit', element: <ProtectedEditor /> },
      { path: '/next', element: <h1>Следующий экран</h1> },
    ], { initialEntries: ['/edit'] });

    render(<RouterProvider router={router} />);
    fireEvent.click(screen.getByRole('link', { name: 'Перейти дальше' }));

    await waitFor(() => expect(confirm).toHaveBeenCalledOnce());
    expect(router.state.location.pathname).toBe('/edit');
    expect(screen.getByDisplayValue('Несохранённый текст')).toBeInTheDocument();
  });

  it('announces and focuses a completed action', async () => {
    render(<ActionReceipt message="Обращение распределено. Открыта следующая задача." />);

    const receipt = screen.getByRole('status');
    expect(receipt).toHaveTextContent('Действие выполнено');
    expect(receipt).toHaveTextContent('Обращение распределено');
    await waitFor(() => expect(receipt).toHaveFocus());
  });
});

function FocusedDetail() {
  const { itemId } = useParams();
  return (
    <ResponsiveMasterDetail className="test-workspace" hasDetail focusKey={itemId}>
      <section>Список</section>
      <section><h1 data-detail-heading tabIndex={-1}>Карточка {itemId}</h1></section>
    </ResponsiveMasterDetail>
  );
}

function ProtectedEditor() {
  return (
    <>
      <UnsavedChangesGuard when />
      <label>Черновик<input defaultValue="Несохранённый текст" /></label>
      <Link to="/next">Перейти дальше</Link>
    </>
  );
}
